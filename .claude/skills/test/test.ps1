# Runs JuliaSynth's tests: whole areas (folders of JuliaSynth.Tests), single classes or single tests, or all of them.
#
#   test.ps1 -Area Streams, Runs
#   test.ps1 -Test ZipTests, LoopTests.Values_go_on_after_a_file
#   test.ps1 -Area Editing -Wsl        # in the WSL clone; sync it first (build skill)
#   test.ps1                           # everything
param(
    [string[]] $Area = @(),
    [string[]] $Test = @(),
    [switch] $Wsl
)

$ErrorActionPreference = 'Stop'
$repo = Resolve-Path "$PSScriptRoot/../../.."
$areas = Get-ChildItem -Directory "$repo/JuliaSynth.Tests" |
    Where-Object { $_.Name -notin 'bin', 'obj' } | ForEach-Object Name

foreach ($a in $Area) {
    if ($a -notin $areas) { throw "No test area '$a'. Areas: $($areas -join ', ')" }
}

# A leading dot keeps LoopTests from matching LoopFeatureTests.
$parts = @($Area | ForEach-Object { "FullyQualifiedName~JuliaSynth.Tests.$_." }) +
         @($Test | ForEach-Object { "FullyQualifiedName~.$_" })
$filter = $parts -join '|'

if ($Wsl) {
    $script = (wsl -e wslpath -a ("$repo" -replace '\\', '/')) + '/.claude/skills/build/wsl.sh'
    if ($filter) { wsl -e bash $script test $filter } else { wsl -e bash $script test }
}
elseif ($filter) {
    dotnet test "$repo/JuliaSynth.Tests" --filter $filter
}
else {
    dotnet test "$repo/JuliaSynth.Tests"
}
exit $LASTEXITCODE
