# Order Processing Service

A REST API to process order calls by downloading reference files, saving them locally, and updating metadata.

---

## Requirements

1. REST API that:
   - Receives an order call with metadata (`Brand`, `Variant`, etc.) and links to files (1-100 MB).
   - Stores metadata in a database.
   - Downloads files and organizes them into subfolders based on `Brand` and `Variant`.
   - Handles errors when files are missing.
   - Confirms receipt of the order.
   - Sends a post-processing "File received" update.

---

## Notes

1. The API and Handler are designed as independent services, enabling deployment either locally on a single machine or across multiple nodes in a distributed setup.
2. In distributed environments, the IRepository and ISignal interfaces must be implemented using scalable technologies, such as a distributed database or messaging system.
3. SQLite is used for simple testing, paired with semaphore-based signals to minimize locking issues, as SQLite does not support long polling.
4. The current semaphore-based signaling is optimized for Windows. On Linux, it defaults to a timeout mechanism. Future plans include adding SignalLocalLinux (e.g., H.Pipes) for local scenarios and Redis or a message broker for distributed setups.

---

## Setup

1. Clone the repository.
2. Set the connection string:
   ```
   export ConnectionStrings__SQLite="Data Source=/path/orders.db;"
   ```
3. Start the API:
   ```
   cd Order.Api
   dotnet run --configuration Release
   ```
4. Start the handler:
   ```
   cd Order.Handler
   dotnet run --configuration Release
   ```
5. Test with .http:
   ```
   POST https://localhost:7152/order
   Content-Type: application/json
   
   {
     "brand": "Nike",
     "variant": "AirMax",
     "netContent": "1L",
     "orderNeed": "Restock",
     "fileLinks": [
       "https://raw.githubusercontent.com/github/gitignore/main/README.md",
       "https://example.com/this-file-does-not-exist"
     ]
   }
   ```

