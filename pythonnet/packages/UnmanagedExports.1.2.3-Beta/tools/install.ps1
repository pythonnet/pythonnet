param($installPath, $toolsPath, $package, $project)

$targetFileName = 'RGiesecke.DllExport.targets'
$targetFileName = [IO.Path]::Combine($toolsPath, $targetFileName)
$targetUri = New-Object Uri -ArgumentList $targetFileName, [UriKind]::Absolute

Add-Type -AssemblyName 'Microsoft.Build, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'

# change the reference to RGiesecke.DllExport.Metadata.dll to not be copied locally

$project.Object.References | ? { 
	$_.Name -eq "RGiesecke.DllExport.Metadata" 
} | % {
	if($_ | Get-Member | ? {$_.Name -eq "CopyLocal"}){
		$_.CopyLocal = $false
	}
}

$projects =  [Microsoft.Build.Evaluation.ProjectCollection]::GlobalProjectCollection.GetLoadedProjects($project.FullName)
$projects |  % {
	$currentProject = $_

	# remove imports of RGiesecke.DllExport.targets from this project 
	$currentProject.Xml.Imports | ? {
		return ("RGiesecke.DllExport.targets" -eq [IO.Path]::GetFileName($_.Project))
	}  | % {  
		$currentProject.Xml.RemoveChild($_);
	}

	# remove the properties DllExportAttributeFullName and DllExportAttributeAssemblyName
	$currentProject.Xml.Properties | ? {
		$_.Name -eq "DllExportAttributeFullName" -or $_.Name -eq "DllExportAttributeAssemblyName"
	} | % {
		$_.Parent.RemoveChild($_)
	}

	$projectUri = New-Object Uri -ArgumentList $currentProject.FullPath, [UriKind]::Absolute
	$relativeUrl = $projectUri.MakeRelative($targetUri)
	[Void]$currentProject.Xml.AddImport($relativeUrl)

	# remove the old stuff in the DllExports folder from previous versions, (will check that only known files are in it)
	Remove-OldDllExportFolder $project
}