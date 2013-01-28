param($installPath, $toolsPath, $package, $project)

$targetFileName = 'RGiesecke.DllExport.targets'
$targetFileName = [System.IO.Path]::Combine($toolsPath, $targetFileName)
$targetUri = New-Object Uri -ArgumentList $targetFileName, [UriKind]::Absolute

Add-Type -AssemblyName 'Microsoft.Build, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'

$projects = [Microsoft.Build.Evaluation.ProjectCollection]::GlobalProjectCollection.GetLoadedProjects($project.FullName)

return $projects |  % {
	$currentProject = $_

	$currentProject.Xml.Imports | ? {
		return ("RGiesecke.DllExport.targets" -eq [System.IO.Path]::GetFileName($_.Project))
	}  | % {  
		$currentProject.Xml.RemoveChild($_)
	}
}