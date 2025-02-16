# SipMiddlewire

<span class="colour" style="color: rgb(204, 204, 204);">A thread-safe C# SIP client middleware for VoIP, featuring asynchronous processing for better performance, reliable SIP communication with error handling</span>
<br>

## Architecture

* Uses Entity Framework with SQLite
* Event-based architecture for call and account state management
* Thread-safe implementations using locks
* Singleton pattern for core services

## Features

* SIP call management (make/receive/hold/transfer)
* Account registration and authentication
* Call logging and history
* DTMF support
* Audio device management
* Database storage using SQLite
* System tray integration

## Integration 

```csharp
// Example usage of async/await pattern
public async Task<bool> InitiateCall(string destination)
{
    await using var call = await _sipManager.CreateCallAsync(destination);
    return await call.ConnectAsync();
}
```

```csharp
// Simple integration with dependency injection
services.AddSingleton<ISipManager, SipManager>();
services.AddScoped<ICallManager, CallManager>();
services.AddScoped<IAccountManager, AccountManager>();
```

