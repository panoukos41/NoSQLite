[CmdletBinding()]
param (
    [Parameter(Position = 0)]
    [ValidateSet("Debug", "Release")]
    [string] $configuration = "Release"
)

$projects = @(
    #"./test/NoSQLite.Test"
)

foreach ($project in $projects) {
    dotnet `
        test $project `
        -c $configuration `
        --no-restore `
        --no-build
}