$SolutionFolderPath = $PSScriptRoot 
cd $SolutionFolderPath

$sign = Read-Host 'Enter nuget sign'

cd .\NLogTools\bin\Release
nuget push DashboardCode.NLogTools.3.0.5.nupkg $sign -Source https://api.nuget.org/v3/index.json

