# Notes: `builder.Services` (Dependency Injection Registration)

## Core Idea
- `builder.Services` **is the DI container** — a single list of "recipes" for creating objects.
- **Every** `builder.Services.AddXxx(...)` call just **adds registrations to that same list**.
- Nothing runs at registration time. The list is used **later** (after `builder.Build()`) to create objects on demand.
- Mental model: it's like writing a **shopping list** — you're not cooking yet, just recording ingredients and how to make each dish.

---

## One List, Three Kinds of Entries

### ?? Group 1 — Register YOUR services (you pick the lifetime)
Take **your** interfaces/classes. **You** choose the lifetime.

**Syntax:**
```csharp
builder.Services.Add<Lifetime><IInterface, Implementation>();
```

**Example:**
```csharp
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddSingleton<BlobServiceClient>(...);
```

**Factory example (custom construction logic):**
```csharp
builder.Services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<IOptions<AzureBlobOptions>>().Value;
    return new BlobServiceClient(options.ConnectionString);
});
```

| Method | Lifetime | Meaning |
|--------|----------|---------|
| `AddTransient<I, T>()` | new every time | per injection |
| `AddScoped<I, T>()` | one per request | per HTTP request |
| `AddSingleton<I, T>()` | one for the app | shared for app lifetime |

?? **This is the only group where the lifetime is YOUR decision.**

---

### ?? Group 2 — Framework "feature bundles" (lifetime handled for you)
Pre-packaged registrations from .NET or libraries. They internally add **many** services
with lifetimes **already decided**. You do **NOT** pick Scoped/Transient here.

**Syntax:**
```csharp
builder.Services.Add<FeatureName>(optionalConfig);
```

**Example:**
```csharp
builder.Services.AddControllers();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));       // registered as Scoped internally
builder.Services.AddAuthentication(...);
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(...);
builder.Services.AddCors(...);
builder.Services.AddAutoMapper(...);
```
- Naming convention: `Add<FeatureName>`.
- Behind the scenes each one does a bunch of `AddSingleton/AddScoped/AddTransient` for you.
- Example: `AddDbContext` registers the context as **Scoped** — that's why we never wrote "Scoped" for it.

---

### ?? Group 3 — Configuration helpers (register settings/options)
Don't register a callable "service" — they register **configuration data**.

**Syntax:**
```csharp
builder.Services.Configure<TOptions>(configSection);
// or the fluent builder form
builder.Services.AddOptions<TOptions>().Bind(configSection).Validate(...).ValidateOnStart();
```

**Example:**
```csharp
builder.Services.AddOptions<AzureBlobOptions>()
    .Bind(builder.Configuration.GetSection(AzureBlobOptions.SectionName))
    .Validate(o => !string.IsNullOrEmpty(o.ConnectionString), "Connection string missing.")
    .ValidateOnStart();
```
- Feed values into the **Options system**, which then hands you `IOptions<T>`.
- `AddOptions` does **not** take a lifetime — the framework pre-registers the options
  interfaces with fixed lifetimes:
  - `IOptions<T>` ? **Singleton**
  - `IOptionsSnapshot<T>` ? **Scoped**
  - `IOptionsMonitor<T>` ? **Singleton** (with change notifications)

---

## Our `Program.cs` Lines by Group

| Line | Group | Who picks lifetime? |
|------|-------|---------------------|
| `AddControllers()` | ?? Feature bundle | Framework |
| `AddDbContext<AppDbContext>(...)` | ?? Feature bundle | Framework (Scoped) |
| `AddScoped<IUserRepository, UserRepository>()` | ?? Your service | **You** (Scoped) |
| `AddScoped<ICartService, CartService>()` | ?? Your service | **You** (Scoped) |
| `AddScoped<IOrderedItemService, OrderedItemService>()` | ?? Your service | **You** (Scoped) |
| `AddOptions<AzureBlobOptions>()...` | ?? Config helper | Framework |
| `AddAutoMapper(...)` | ?? Feature bundle | Framework |
| `AddAuthentication(...)` / `AddAuthorization()` | ?? Feature bundle | Framework |
| `AddRateLimiter(...)` | ?? Feature bundle | Framework |
| `AddCors(...)` | ?? Feature bundle | Framework |

---

## The Mental Model
```
builder.Services  =  a single list of registrations (the DI container)

     ?? AddScoped/AddTransient/AddSingleton  ? register YOUR classes (you set lifetime)
     ?? AddControllers/AddDbContext/AddCors… ? framework bundles (lifetime built-in)
     ?? AddOptions/Configure                 ? register settings/config

Then:  var app = builder.Build();   ? the list is "frozen" and used to build objects on demand
```

---

## Key Takeaways
1. All `AddXxx` calls add to the **same container** — just different *kinds* of entries.
2. You only choose a **lifetime** for **your own** classes (Group 1).
3. `Add<Feature>()` methods are **shortcuts** that register lots of stuff with lifetimes
   already set — that's why they *look* different.
4. Nothing executes at registration time; it's all recorded and used later.
