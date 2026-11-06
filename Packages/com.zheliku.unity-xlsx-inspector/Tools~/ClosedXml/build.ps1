param([string]$BuildDirectory = (Join-Path ([IO.Path]::GetTempPath()) ('unity-xlsx-closedxml-' + [Guid]::NewGuid().ToString('N'))))

$ErrorActionPreference = 'Stop'
$packageRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$archive = Join-Path $BuildDirectory 'ClosedXML-0.105.1.zip'
New-Item -ItemType Directory -Path $BuildDirectory -Force | Out-Null
Invoke-WebRequest 'https://codeload.github.com/ClosedXML/ClosedXML/zip/refs/tags/0.105.1' -OutFile $archive
Expand-Archive -LiteralPath $archive -DestinationPath $BuildDirectory
$sourceRoot = Join-Path $BuildDirectory 'ClosedXML-0.105.1'
$project = Join-Path $sourceRoot 'ClosedXML/ClosedXML.csproj'

# 只替换图形后端，移除字体包引用与内嵌字体。其余表格、图片读取及公式代码保留上游版本。
[xml]$projectXml = Get-Content -LiteralPath $project
foreach ($node in @($projectXml.SelectNodes('//EmbeddedResource | //PackageReference[@Include="SixLabors.Fonts"]'))) {
    $node.ParentNode.RemoveChild($node) | Out-Null
}
$projectXml.Save($project)
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'DefaultGraphicEngine.cs') -Destination (Join-Path $sourceRoot 'ClosedXML/Graphics/DefaultGraphicEngine.cs')
dotnet build $project --configuration Release --framework netstandard2.1 --nologo `
    -p:GeneratePackageOnBuild=false -p:NuGetAudit=false -p:ContinuousIntegrationBuild=true -p:InformationalVersion=0.105.1-unity-xlsx.1
if ($LASTEXITCODE -ne 0) { throw 'ClosedXML build failed.' }
Copy-Item -LiteralPath (Join-Path $sourceRoot 'ClosedXML/bin/Release/netstandard2.1/ClosedXML.dll') -Destination (Join-Path $packageRoot 'Editor/Plugins/ClosedXML.dll')
Write-Output 'Updated the MIT-only ClosedXML build. No font assemblies or font files are embedded.'
