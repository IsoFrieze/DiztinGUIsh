    # based on: https://softwareengineering.stackexchange.com/questions/235082/get-license-information-for-all-used-nuget-packages
    # Run in Package Manager Console with `./download-packages-license.ps1`.
    # If access denied, execute `Set-ExecutionPolicy -Scope Process -ExecutionPolicy RemoteSigned`.
    
    $outputpath = ".\DiztinGUIsh\bin\Release\net5.0-windows\publish\licenses\";
    
    Split-Path -parent $dte.Solution.FileName | cd; New-Item -ItemType Directory -Force -Path $outputpath;
    @( Get-Project -All | ? { $_.ProjectName } | % {
        Get-Package -ProjectName $_.ProjectName | ? { $_.LicenseUrl }
    } ) | Sort-Object Id -Unique | % {
        $pkg = $_;
        Try {
            if ($pkg.Id -notlike 'microsoft*' -and $pkg.LicenseUrl.StartsWith('http')) {
                Write-Host ("Download license for package " + $pkg.Id + " from " + $pkg.LicenseUrl);
                #Write-Host (ConvertTo-Json ($pkg));
    
                $licenseUrl = $pkg.LicenseUrl
                if ($licenseUrl.contains('github.com')) {
                    $licenseUrl = $licenseUrl.replace("/blob/", "/raw/")
                }
    
                $extension = ".txt"
                if ($licenseUrl.EndsWith(".md")) {
                    $extension = ".md"
                }
                
                $pathFileNoExt = (Join-Path (pwd) $outputpath) + $pkg.Id;
                $downloadedFile = $pathFileNoExt + $extension;
                # Write-Host ("-- into: " + $downloadedFile);
                
                (New-Object System.Net.WebClient).DownloadFile($licenseUrl, $downloadedFile);
    
                if ((Get-ChildItem $downloadedFile | Select-String -Pattern '<html').count -gt 0)
                {
                    $newHtmlFile = $pathFileNoExt + ".html";
                    # Write-Host ("-- looks like html, so rename to: " + $newHtmlFile);
                    Move-Item -Force $downloadedFile -Destination $newHtmlFile;
                }
            }
        }
        Catch [system.exception] {
            Write-Host ("Could not download license for " + $pkg.Id)
        }
    }