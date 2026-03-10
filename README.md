The project is intended for smart home aplications where LLM ai is being integrated. The software simplifies rest api comunication and saves RAM of clients.

This software handles: comunication with ollama server, handling message history for individual clients(microcontrolers)

there is three endpoints:
POST /api/newchat    returun: guid of the user as raw text
POST /api/message    client message - raw text - clientID(guid):message for ai
PUT /api/model        client message - raw text - clientID(guid):model       (the model must be previoously installed on ollama server)


The software saves data into postgredatabase

example docker compose(included on git in the project):

services:
  simpleaiapi:
    image: simpleaiapi
    container_name: simpleaiapi
    restart: on-failure
    build:
      context: .
      dockerfile: SimpleAIAPI/Dockerfile
    environment:
      # Use the service name 'ollama' instead of 127.0.0.1
      - OLLAMA_IP=ollama:11434
      - ConnectionStrings__DefaultConnection=Host=db;Database=ai_db;Username=postgres;Password=postgres
    depends_on:
      - db
      - ollama
    ports:
      - "8080:8080"

  db:
    image: postgres:17-alpine # Note: Postgres 18 is not yet released/stable; 17 is current.
    container_name: db
    environment:
      - POSTGRES_DB=ai_db
      - POSTGRES_USER=postgres
      - POSTGRES_PASSWORD=postgres
    ports:
      - "5432:5432"

  ollama:
    image: ollama/ollama:latest
    container_name: ollama
    # If using GPU, you'd add deploy/resources here
    volumes:
      - ollama_data:/root/.ollama
    ports:
      - "11435:11434"

volumes:
  ollama_data:
