param(
    [string]$BaseUrl = "http://localhost:5000",
    [string]$Docker = "docker",
    [switch]$ExerciseOutage
)
$ErrorActionPreference = "Stop"
function Assert-Equal($actual, $expected, $message) {
    if ($actual -ne $expected) { throw "$message Expected=$expected Actual=$actual" }
}
function Wait-Cashback($userId, $expected) {
    $deadline = (Get-Date).AddSeconds(90)
    do {
        $summary = Invoke-RestMethod "$BaseUrl/api/users/$userId/cashback"
        if ($summary.totalCashback -eq $expected) { return }
        Start-Sleep -Milliseconds 500
    } while ((Get-Date) -lt $deadline)
    throw "Cashback did not reach $expected within 90 seconds."
}
Assert-Equal (Invoke-WebRequest "$BaseUrl/swagger/index.html").StatusCode 200 "Swagger unavailable."
$user = Invoke-RestMethod "$BaseUrl/api/users" -Method Post -ContentType application/json -Body '{"name":"Smoke Test"}'
$rules = Invoke-RestMethod "$BaseUrl/api/cashback-rules"
$rule = $rules | Where-Object { $_.category -eq 'Restaurants' }
if (-not $rule.isActive -or $rule.monthlyLimit -lt 250 -or $rule.percentage -ne 5) {
    throw "Smoke test expects the seeded Restaurants rule: active, 5%, limit >= 250."
}
$brokerStopped = $false
try {
    if ($ExerciseOutage) {
        & $Docker compose stop rabbitmq
        if ($LASTEXITCODE -ne 0) { throw "Could not stop broker." }
        $brokerStopped = $true
    }
    $purchase = Invoke-RestMethod "$BaseUrl/api/transactions" -Method Post -ContentType application/json -Body (
        @{ userId = $user.id; amount = 5000; category = "Restaurants"; merchant = "Smoke Restaurant" } | ConvertTo-Json
    )
    if ($ExerciseOutage) {
        Start-Sleep -Seconds 7
        $summary = Invoke-RestMethod "$BaseUrl/api/users/$($user.id)/cashback"
        Assert-Equal $summary.totalCashback 0 "Reward should wait while broker is stopped."
        & $Docker compose start rabbitmq
        if ($LASTEXITCODE -ne 0) { throw "Could not restart broker." }
        $brokerStopped = $false
    }
    Wait-Cashback $user.id 250
    # Same transaction, new event ID, sent twice: both forms of duplicate protection.
    $eventId = [guid]::NewGuid().ToString()
    $payload = @{
        EventId = $eventId; TransactionId = $purchase.id; UserId = $user.id
        Amount = 5000; Category = 1; CreatedAt = $purchase.createdAt
    } | ConvertTo-Json -Compress
    $auth = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes("rewardengine:rewardengine_dev"))
    $body = @{properties=@{delivery_mode=2};routing_key="transaction-created";payload=$payload;payload_encoding="string"} | ConvertTo-Json
    1..2 | ForEach-Object {
        $result = Invoke-RestMethod "http://localhost:15672/api/exchanges/%2F/amq.default/publish" -Method Post -Headers @{Authorization="Basic $auth"} -ContentType application/json -Body $body
        Assert-Equal $result.routed $true "Duplicate event was not routed."
    }
    Start-Sleep -Seconds 5
    $history = Invoke-RestMethod "$BaseUrl/api/users/$($user.id)/cashback/history"
    Assert-Equal @($history).Count 1 "Duplicate reward detected."
    Assert-Equal $history[0].cashbackAmount 250 "Incorrect reward."
    $transaction = Invoke-RestMethod "$BaseUrl/api/transactions/$($purchase.id)"
    Assert-Equal $transaction.status "Rewarded" "Purchase was not marked rewarded."
    Write-Output "PASS: Swagger, purchase, Outbox, RabbitMQ, Worker, cashback=250, history, duplicate delivery. UserId=$($user.id); EventId=$eventId; Outage=$ExerciseOutage"
}
finally {
    if ($brokerStopped) { & $Docker compose start rabbitmq }
}
