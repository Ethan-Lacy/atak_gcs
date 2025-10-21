# PowerShell script to inspect IChatRoom interface
$dllPath = "C:\Users\ethan\Documents\Coding\Wintak_Plugins\WinTAK Plugin (5.0)_voice\WinTAK Plugin (5.0)_voice\bin\x64\Debug\WinTak.Net.dll"

if (Test-Path $dllPath) {
    Write-Host "Loading assembly: $dllPath" -ForegroundColor Green
    $assembly = [System.Reflection.Assembly]::LoadFile($dllPath)

    # Find IChatRoom interface
    $chatRoomInterface = $assembly.GetTypes() | Where-Object { $_.Name -eq "IChatRoom" }

    if ($chatRoomInterface) {
        Write-Host "`n=== IChatRoom Interface ===" -ForegroundColor Cyan
        Write-Host "Full Name: $($chatRoomInterface.FullName)" -ForegroundColor Yellow

        Write-Host "`n--- Properties ---" -ForegroundColor Green
        $chatRoomInterface.GetProperties() | ForEach-Object {
            Write-Host "  $($_.PropertyType.Name) $($_.Name) { get; }" -ForegroundColor White
        }

        Write-Host "`n--- Methods ---" -ForegroundColor Green
        $chatRoomInterface.GetMethods() | Where-Object { !$_.IsSpecialName } | ForEach-Object {
            $params = ($_.GetParameters() | ForEach-Object { "$($_.ParameterType.Name) $($_.Name)" }) -join ", "
            Write-Host "  $($_.ReturnType.Name) $($_.Name)($params)" -ForegroundColor White
        }
    } else {
        Write-Host "IChatRoom interface not found!" -ForegroundColor Red
    }

    # Also find IChatService
    $chatServiceInterface = $assembly.GetTypes() | Where-Object { $_.Name -eq "IChatService" }
    if ($chatServiceInterface) {
        Write-Host "`n`n=== IChatService Interface ===" -ForegroundColor Cyan
        Write-Host "Full Name: $($chatServiceInterface.FullName)" -ForegroundColor Yellow

        Write-Host "`n--- Properties ---" -ForegroundColor Green
        $chatServiceInterface.GetProperties() | ForEach-Object {
            Write-Host "  $($_.PropertyType.Name) $($_.Name)" -ForegroundColor White
        }

        Write-Host "`n--- Methods ---" -ForegroundColor Green
        $chatServiceInterface.GetMethods() | Where-Object { !$_.IsSpecialName } | ForEach-Object {
            $params = ($_.GetParameters() | ForEach-Object { "$($_.ParameterType.Name) $($_.Name)" }) -join ", "
            Write-Host "  $($_.ReturnType.Name) $($_.Name)($params)" -ForegroundColor White
        }
    }

    # Find Message class
    $messageClass = $assembly.GetTypes() | Where-Object { $_.Name -eq "Message" }
    if ($messageClass) {
        Write-Host "`n`n=== Message Class ===" -ForegroundColor Cyan
        Write-Host "Full Name: $($messageClass.FullName)" -ForegroundColor Yellow

        Write-Host "`n--- Constructors ---" -ForegroundColor Green
        $messageClass.GetConstructors() | ForEach-Object {
            $params = ($_.GetParameters() | ForEach-Object { "$($_.ParameterType.Name) $($_.Name)" }) -join ", "
            Write-Host "  Message($params)" -ForegroundColor White
        }

        Write-Host "`n--- Properties ---" -ForegroundColor Green
        $messageClass.GetProperties() | ForEach-Object {
            Write-Host "  $($_.PropertyType.Name) $($_.Name)" -ForegroundColor White
        }
    }

} else {
    Write-Host "DLL not found at: $dllPath" -ForegroundColor Red
}
