The project is intended for smart home aplications where LLM ai is being integrated. The software simplifies rest api comunication and saves RAM of clients.

This software handles: comunication with ollama server, handling message history for individual clients(microcontrolers)

there is three endpoints:

POST /api/newchat    returun: guid of the user as raw text

POST /api/message    client message - raw text - clientID(guid):message for ai

PUT /api/model        client message - raw text - clientID(guid):model       (the model must be previoously installed on ollama server)


The software saves data into postgredatabase

example docker compose(included on git in the project root folder)
