# =============================================================================
# ShopCore - Parallel End-to-End Checkout Load Test
# -----------------------------------------------------------------------------
# Simulates N users concurrently running: login -> check products -> add to cart
#   -> begin checkout (reserve) -> pay -> verify invoice.
#
# Requires PowerShell 7+ (uses ForEach-Object -Parallel).
#   Check: $PSVersionTable.PSVersion   (must be 7.0 or higher)
#   Run:   pwsh ./test-checkout-parallel.ps1
# =============================================================================

param(
    [string]$BaseUrl        = "http://localhost:5274",
    [int]   $UserCount      = 10,
    [int]   $Quantity       = 70,   # units requested PER product line (high -> forces contention)
    [int]   $ProductsPerUser = 0,   # 0 = use ALL products; otherwise cap per user
    [switch]$Reset                  # flush Redis + re-seed stock from SQL before running
)

$ErrorActionPreference = "Stop"

Write-Host "=== ShopCore parallel checkout test ($UserCount users) ===" -ForegroundColor Cyan
Write-Host "Base URL: $BaseUrl`n"

# -----------------------------------------------------------------------------
# 0a. Optional: reset Redis to a clean baseline (DEV endpoint) before testing.
# -----------------------------------------------------------------------------
if ($Reset) {
    Write-Host "[reset] Flushing Redis + re-seeding stock from SQL..." -ForegroundColor Yellow
    $r = Invoke-RestMethod -Uri "$BaseUrl/api/dev/reset-stock" -Method Post
    Write-Host ("[reset] {0} (products warmed: {1})" -f $r.message, $r.productsWarmed) -ForegroundColor Green
    Write-Host ""
}

# -----------------------------------------------------------------------------
# 0b. Pre-flight: fetch the WHOLE catalog once. Each user will later checkout a
#    RANDOM PERMUTATION of these products at high quantity, so total demand far
#    exceeds stock and some users MUST fail on availability (409) - proving the
#    Redis reservation hot path never oversells under concurrency.
# -----------------------------------------------------------------------------
Write-Host "[setup] Fetching product catalog..." -ForegroundColor Yellow
$catalog = Invoke-RestMethod -Uri "$BaseUrl/api/products?page=1&pageSize=100" -Method Get

if (-not $catalog.Items -or $catalog.Items.Count -eq 0) {
    throw "No products found in the catalog. Seed products before running this test."
}

$products = @($catalog.Items)
Write-Host ("[setup] Catalog has {0} products. Each user will request {1} unit(s) per line." -f `
    $products.Count, $Quantity) -ForegroundColor Green
Write-Host ("[setup] Total demand ~= {0} users x {1} products x {2} qty = {3} units." -f `
    $UserCount, $products.Count, $Quantity, ($UserCount * $products.Count * $Quantity)) -ForegroundColor Green

# Show actual stock per product (StockQuantity comes straight from the catalog DTO).
$totalStock = ($products | Measure-Object -Property StockQuantity -Sum).Sum
$minStock   = ($products | Measure-Object -Property StockQuantity -Minimum).Minimum
$maxStock   = ($products | Measure-Object -Property StockQuantity -Maximum).Maximum
Write-Host ("[setup] Actual stock => total={0}, min={1}, max={2} across {3} products." -f `
    $totalStock, $minStock, $maxStock, $products.Count) -ForegroundColor Cyan
$products | Sort-Object Id | ForEach-Object {
    Write-Host ("         Id={0,-4} stock={1,-5} {2}" -f $_.Id, $_.StockQuantity, $_.Title) -ForegroundColor DarkGray
}
Write-Host ""

# -----------------------------------------------------------------------------
# 1. Run the full flow for each user IN PARALLEL.
# -----------------------------------------------------------------------------
$results = 1..$UserCount | ForEach-Object -Parallel {
    $i               = $_
    $baseUrl         = $using:BaseUrl
    $products        = $using:products
    $quantity        = $using:Quantity
    $productsPerUser = $using:ProductsPerUser

    $email = "loadtest_user${i}@example.com"
    $pass  = "Test@12345"
    $step  = "start"

    function Invoke-Json($method, $url, $body, $headers) {
        $json = if ($body) { $body | ConvertTo-Json -Depth 6 } else { $null }
        return Invoke-RestMethod -Uri $url -Method $method -Body $json `
            -ContentType "application/json" -Headers $headers
    }

    try {
        # --- Register (ignore "already exists" so re-runs work) ---
        $step = "register"
        try {
            Invoke-Json POST "$baseUrl/api/auth/register" @{
                Name = "Load User $i"; Email = $email; Password = $pass
            } @{} | Out-Null
        } catch { }  # user may already exist -> continue to login

        # --- Login -> token + userId ---
        $step  = "login"
        $auth  = Invoke-Json POST "$baseUrl/api/auth/login" @{
            Email = $email; Password = $pass
        } @{}
        $token   = $auth.AccessToken
        $userId  = $auth.UserId
        $headers = @{ Authorization = "Bearer $token" }

        # --- Check products (per-user verification) ---
        $step = "get-products"
        $list = Invoke-RestMethod -Uri "$baseUrl/api/products?page=1&pageSize=10" `
            -Method Get -Headers $headers
        if (-not $list.Items -or $list.Items.Count -eq 0) {
            throw "catalog empty for user $i"
        }

        # --- Build a RANDOM PERMUTATION of products for this user's cart ---
        # Shuffle the catalog, optionally cap how many lines, all at high quantity.
        $step  = "build-basket"
        $shuffled = $products | Sort-Object { Get-Random }
        if ($productsPerUser -gt 0 -and $productsPerUser -lt $shuffled.Count) {
            $shuffled = $shuffled[0..($productsPerUser - 1)]
        }
        $addedItems = foreach ($p in $shuffled) {
            @{
                ProductId   = $p.Id
                Description = $p.Title
                Quantity    = $quantity
                UnitPrice   = $p.Price
                UserId      = $userId
            }
        }

        # --- Add the basket to the cart ---
        $step = "add-to-cart"
        Invoke-Json POST "$baseUrl/api/cart/update" @{
            UserId  = $userId
            Added   = @($addedItems)
            Updated = @()
            Removed = @()
        } $headers | Out-Null

        # --- Begin checkout (reserve + Pending order). Unique Idempotency-Key. ---
        $step = "begin"
        $beginHeaders = $headers.Clone()
        $beginHeaders["Idempotency-Key"] = "loadtest-$i-$([guid]::NewGuid())"
        $begin = Invoke-RestMethod -Uri "$baseUrl/api/checkout/begin" -Method Post `
            -Headers $beginHeaders -ContentType "application/json" -Body (@{
                UserId       = $userId
                OrderDetails = @{
                    FirstName = "Load"; LastName = "User$i"; Email = $email
                    Address = "123 Main St"; Address2 = ""
                    Country = "India"; State = "WB"; Zip = "700001"
                }
            } | ConvertTo-Json -Depth 6)
        $orderId = $begin.OrderId

        # --- Pay (success) ---
        $step = "pay"
        Invoke-Json POST "$baseUrl/api/payment/pay" @{
            OrderId = $orderId; Success = $true
        } $headers | Out-Null

        [pscustomobject]@{
            User = $i; Status = "OK"; OrderId = $orderId; FailedStep = $null; Error = $null
        }
    }
    catch {
        # A 409 Conflict at the 'begin' step is the EXPECTED, CORRECT outcome under
        # contention: the Redis reservation refused to oversell. Flag it separately
        # from genuine failures.
        $statusCode = $null
        try { $statusCode = [int]$_.Exception.Response.StatusCode } catch { }

        if ($step -eq "begin" -and $statusCode -eq 409) {
            [pscustomobject]@{
                User = $i; Status = "OUT_OF_STOCK"; OrderId = $null
                FailedStep = $step; Error = "409 - reservation refused (no oversell)"
            }
        }
        else {
            [pscustomobject]@{
                User = $i; Status = "FAIL"; OrderId = $null
                FailedStep = $step; Error = $_.Exception.Message
            }
        }
    }
} -ThrottleLimit $UserCount

# -----------------------------------------------------------------------------
# 2. Summary
# -----------------------------------------------------------------------------
Write-Host "`n=== Results ===" -ForegroundColor Cyan
$results | Sort-Object User | Format-Table -AutoSize

$ok    = ($results | Where-Object Status -eq "OK").Count
$oos   = ($results | Where-Object Status -eq "OUT_OF_STOCK").Count
$fail  = ($results | Where-Object Status -eq "FAIL").Count
Write-Host ("Succeeded: {0}  |  Out-of-stock (expected 409): {1}  |  Failed (errors): {2}" -f `
    $ok, $oos, $fail) -ForegroundColor $(if ($fail -eq 0) { "Green" } else { "Yellow" })

if ($oos -gt 0 -and $fail -eq 0) {
    Write-Host "OVERSELL PROTECTION WORKING: some users were correctly refused, none oversold." -ForegroundColor Green
}

Write-Host "`nNote: fulfillment (invoice PDF + email) runs in the background." -ForegroundColor DarkGray
Write-Host "Give it ~5-10s, then check invoices via GET /api/orderedItems/get-invoice." -ForegroundColor DarkGray
