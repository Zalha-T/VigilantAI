# PowerShell script za testiranje Content Moderation Agent API-ja

$baseUrl = "https://localhost:5001"
$apiUrl = "$baseUrl/api"

# Ignoriraj SSL certificate errors (samo za development)
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = {$true}

Write-Host "=== Content Moderation Agent API Test ===" -ForegroundColor Green
Write-Host ""

# Test 1: Kreiraj čist komentar (trebao bi biti Allow)
Write-Host "Test 1: Kreiranje čistog komentara..." -ForegroundColor Yellow
$cleanComment = @{
    type = 1
    text = "Great article! Very informative and well written."
    authorUsername = "test_user_clean"
    threadId = $null
} | ConvertTo-Json

try {
    $response = Invoke-RestMethod -Uri "$apiUrl/content" -Method Post -Body $cleanComment -ContentType "application/json"
    Write-Host "✓ Komentar kreiran: $($response.contentId)" -ForegroundColor Green
    Write-Host "  Status: $($response.status)" -ForegroundColor Cyan
    $cleanContentId = $response.contentId
} catch {
    Write-Host "✗ Greška: $_" -ForegroundColor Red
}
Write-Host ""

# Čekaj da agent procesira
Write-Host "Čekam 3 sekunde da agent procesira..." -ForegroundColor Yellow
Start-Sleep -Seconds 3
Write-Host ""

# Test 2: Kreiraj spam komentar (trebao bi biti Block)
Write-Host "Test 2: Kreiranje spam komentara..." -ForegroundColor Yellow
$spamComment = @{
    type = 1
    text = "SPAM SPAM SPAM BUY NOW CLICK HERE LIMITED TIME OFFER"
    authorUsername = "test_user_spam"
    threadId = $null
} | ConvertTo-Json

try {
    $response = Invoke-RestMethod -Uri "$apiUrl/content" -Method Post -Body $spamComment -ContentType "application/json"
    Write-Host "✓ Komentar kreiran: $($response.contentId)" -ForegroundColor Green
    Write-Host "  Status: $($response.status)" -ForegroundColor Cyan
    $spamContentId = $response.contentId
} catch {
    Write-Host "✗ Greška: $_" -ForegroundColor Red
}
Write-Host ""

# Čekaj da agent procesira
Write-Host "Čekam 3 sekunde da agent procesira..." -ForegroundColor Yellow
Start-Sleep -Seconds 3
Write-Host ""

# Test 3: Provjeri pending review queue
Write-Host "Test 3: Provjera pending review queue..." -ForegroundColor Yellow
try {
    $pending = Invoke-RestMethod -Uri "$apiUrl/content/pending-review" -Method Get
    Write-Host "✓ Pronađeno $($pending.Count) komentara u review queue" -ForegroundColor Green
    foreach ($item in $pending) {
        Write-Host "  - ID: $($item.id), Decision: $($item.prediction.decision), Score: $($item.prediction.finalScore)" -ForegroundColor Cyan
    }
} catch {
    Write-Host "✗ Greška: $_" -ForegroundColor Red
}
Write-Host ""

# Test 4: Daj feedback za prvi komentar
if ($cleanContentId) {
    Write-Host "Test 4: Davanje feedbacka za čist komentar..." -ForegroundColor Yellow
    $feedback = @{
        goldLabel = 1  # Allow
        correctDecision = $true
        feedback = "Agent was correct - this is a clean comment"
        moderatorId = $null
    } | ConvertTo-Json

    try {
        $response = Invoke-RestMethod -Uri "$apiUrl/review/$cleanContentId/review" -Method Post -Body $feedback -ContentType "application/json"
        Write-Host "✓ Feedback dan: $($response.reviewId)" -ForegroundColor Green
    } catch {
        Write-Host "✗ Greška: $_" -ForegroundColor Red
    }
    Write-Host ""
}

Write-Host "=== Test završen ===" -ForegroundColor Green
Write-Host ""
Write-Host "Provjeri bazu podataka ili Swagger UI za detalje:" -ForegroundColor Yellow
Write-Host "  Swagger: $baseUrl/swagger" -ForegroundColor Cyan
