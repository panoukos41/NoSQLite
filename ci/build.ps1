[CmdletBinding()]
param (
    [Parameter(Position = 1)]
    [ValidateSet("Debug", "Release")]
    [string] $configuration = "Release"
)

$projects = @(
    "./src/NoSQLite"
    "./test/NoSQLite.Test"
)

foreach ($project in $projects) {
    dotnet `
        build $project `
        -c $configuration `
        -v minimal
}
