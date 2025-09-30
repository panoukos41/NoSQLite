function Invoke-Process($command) {
    if ($command -is [string[]]) {
    }
    elseif ($command -is [string]) {
        $command = $command -split " "
    }
    else {
        throw "Invalid command object. Can only accept string[] or string";
    }

    $filePath = $command[0]
    $argumentList = $command[1..($command.Length - 1)];
    $process = Start-Process -FilePath $filePath -ArgumentList $argumentList -NoNewWindow -Wait
    Write-Host $process
    if ($null -ne $process.ExitCode || $process.ExitCode -ne 0) {
        exit $process.ExitCode
    }
}