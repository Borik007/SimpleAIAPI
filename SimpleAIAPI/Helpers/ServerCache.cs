
using System.Collections.Concurrent;
using SimpleAIAPI.Data;
using Microsoft.EntityFrameworkCore;

namespace SimpleAIAPI.Models;

public class ServerCache : AIServer
{
    private readonly ConcurrentDictionary<Guid, SemaphoreClient> _semaphoreClients = new();

    public int CacheSize { get; }
    public int ChatHistorySize { get; }

    public ServerCache(string aiIp, int cacheSize, int chatHistorySize)
    {
        AI_IP = aiIp;
        CacheSize = cacheSize;
        ChatHistorySize = chatHistorySize;
    }

    public async Task SaveClient(Client client, AIDbContext db)
    {
        var id = client.ClientID;
        var semClient = GetOrCreateSemaphoreClient(id, client);

        await semClient.Semaphore.WaitAsync();
        try
        {
            semClient.Client_IP = client.Client_IP;
            semClient.AI_IP = client.AI_IP;
            semClient.Model = client.Model;
            semClient.Messages = client.Messages;
            semClient.LastUsed = DateTime.UtcNow;

            var tracked = db.ChangeTracker.Entries<Client>()
                .FirstOrDefault(e => e.Entity.ClientID == id);
            if (tracked != null)
            {
                tracked.State = EntityState.Detached;
            }

            var existsInDb = await db.Clients.AsNoTracking().AnyAsync(c => c.ClientID == id);
            if (existsInDb)
            {
                db.Clients.Update(semClient);
            }
            else
            {
                db.Clients.Add(semClient);
            }

            await db.SaveChangesAsync();

            db.Entry(semClient).State = EntityState.Detached;
        }
        finally
        {
            semClient.Semaphore.Release();
        }

        EvictIfNecessary();
    }

    public async Task<Client?> GetClient(Guid id, AIDbContext db)
    {
        if (_semaphoreClients.TryGetValue(id, out var existingClient))
        {
            existingClient.LastUsed = DateTime.UtcNow;
            return existingClient;
        }

        var client = await db.Clients
            .AsNoTracking()
            .Include(c => c.Messages
                .OrderByDescending(m => m.Timestamp)
                .Take(ChatHistorySize))
            .FirstOrDefaultAsync(c => c.ClientID == id);

        if (client == null)
            return null;

        return GetOrCreateSemaphoreClient(id, client);
    }

    private SemaphoreClient GetOrCreateSemaphoreClient(Guid id, Client client)
    {
        // GetOrAdd is atomic — if two threads race, only one factory runs,
        // and both get the same instance back.
        return _semaphoreClients.GetOrAdd(id, _ =>
        {
            EvictIfNecessary();
            return new SemaphoreClient(client) { LastUsed = DateTime.UtcNow };
        });
    }

    /// <summary>
    /// Lock-free O(n) eviction: single pass to find the oldest unlocked client.
    /// Safe to call concurrently — TryRemove guarantees only one thread wins.
    /// </summary>
    private void EvictIfNecessary()
    {
        while (_semaphoreClients.Count >= CacheSize)
        {
            Guid oldestKey = Guid.Empty;
            DateTime oldestTime = DateTime.MaxValue;

            // Single O(n) scan — no sorting
            foreach (var kvp in _semaphoreClients)
            {
                if (kvp.Value.Semaphore.CurrentCount > 0 && kvp.Value.LastUsed < oldestTime)
                {
                    oldestTime = kvp.Value.LastUsed;
                    oldestKey = kvp.Key;
                }
            }

            if (oldestKey != Guid.Empty)
            {
                // TryRemove is atomic; if another thread already removed it, this is a no-op
                _semaphoreClients.TryRemove(oldestKey, out _);
            }
            else
            {
                // All clients are currently locked; can't evict
                break;
            }
        }
    }
}

public class SemaphoreClient : Client
{
    public DateTime LastUsed { get; set; } = DateTime.UtcNow;

    public SemaphoreClient(Client client)
    {
        ClientID = client.ClientID;
        Client_IP = client.Client_IP;
        AI_IP = client.AI_IP;
        Model = client.Model;
        Messages = client.Messages;
    }

    public SemaphoreSlim Semaphore { get; } = new(1, 1);
}