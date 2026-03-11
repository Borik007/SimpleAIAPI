using SimpleAIAPI.Data;
using Microsoft.EntityFrameworkCore;
namespace SimpleAIAPI.Models;

public class ServerCache : AIServer
{
    private SemaphoreSlim _cacheLock = new(1, 1);
    public int CacheSize { get; set; } = 10;
    
    public ServerCache(string aiIp, int cacheSize)
    {
        AI_IP = aiIp;
        CacheSize = cacheSize;
    }
    
    public async Task<string> SaveClient(Client client, AIDbContext db)
    {
        await _cacheLock.WaitAsync();
        try
        {
            var existing = Clients.FirstOrDefault(c => c.ClientID == client.ClientID);
            if (existing != null)
            {
                Clients.Remove(existing);
            }
            
            if (Clients.Count >= CacheSize)
            {
                Clients.RemoveAt(0);
            }
            Clients.Add(client);
            
            db.Clients.Update(client);
            await db.SaveChangesAsync();
        }
        catch (Exception e)
        {
            _cacheLock.Release();
            Console.WriteLine(e);
            throw;
        }
        finally
        {
            _cacheLock.Release();
        }
        await Task.Delay(1000);
        return "Client saved successfully";
    }

    public async Task<Client> GetClient(Guid id, AIDbContext db)
    {
        Client client;
        await _cacheLock.WaitAsync();
        try
        {
            client = Clients.FirstOrDefault(c => c.ClientID == id);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
        finally
        {
            _cacheLock.Release();
        }
        if (client != null) return client;
        else
        {
            client = await db.Clients
                .Include(c => c.Messages
                    .OrderByDescending(m => m.Timestamp)
                    .Take(10).OrderBy(m => m.Timestamp)) 
                .FirstOrDefaultAsync(c => c.ClientID == id);
            
            if (client == null) return null;

            await _cacheLock.WaitAsync();
            try
            {
                if (Clients.Count >= CacheSize)
                {
                    Clients.RemoveAt(0);
                }
                Clients.Add(client);

                _cacheLock.Release();
            }
            catch (Exception e)
            {
                _cacheLock.Release();
                Console.WriteLine(e);
                throw;
                
            }
            return client;
            
        }
    }
    
}