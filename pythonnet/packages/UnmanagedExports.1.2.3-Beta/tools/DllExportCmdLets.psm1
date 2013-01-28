function Remove-OldDllExportFolder
{
	param($project)
	$defaultFiles = ('DllExportAttribute.cs',
	                 'Mono.Cecil.dll',
	                 'RGiesecke.DllExport.dll',
	                 'RGiesecke.DllExport.pdb',
	                 'RGiesecke.DllExport.MSBuild.dll',
	                 'RGiesecke.DllExport.MSBuild.pdb',
	                 'RGiesecke.DllExport.targets')

	$projectFile = New-Object IO.FileInfo($project.FullName)

	$projectFile.Directory.GetDirectories("DllExport") | Select-Object -First 1 | % {
		$dllExportDir = $_
	
		if($dllExportDir.GetDirectories().Count -eq 0){
			$unknownFiles = $dllExportDir.GetFiles() | Select -ExpandProperty Name | ? { -not $defaultFiles -contains $_ }
	
			if(-not $unknownFiles){
				Write-Host "Removing 'DllExport' from " $project.Name
				$project.ProjectItems | ? {	$_.Name -eq 'DllExport' } | % {
					$_.Remove()
				}

				Write-Host "Deleting " $dllExportDir.FullName " ..."
				$dllExportDir.Delete($true)
			}
		}
	}
}

function Remove-OldDllExportFolders
{
	Get-Project -all | % {
		Remove-OldDllExportFolder $_
	}
}

Export-ModuleMember Remove-OldDllExportFolder
Export-ModuleMember Remove-OldDllExportFolders