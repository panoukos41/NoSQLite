[CmdletBinding()]

$projects = @(
    "./src/NoSQLite"
)

$output = "./nuget"

foreach ($project in $projects) {
    dotnet `
        pack $project `
        -c `Release `
        --no-restore `
        --no-build `
        -v minimal `
        -o $output
}
