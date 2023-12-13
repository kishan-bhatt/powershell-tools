param (
    [string]$BitBucketServer
)

# Function to clone and check branches in a repository
function Get-StaleAndMergedBranches($repoUrl) {
    # Clone the repository temporarily
    $repoName = $repoUrl.Split('/')[-1]
    $clonePath = Join-Path $env:TEMP $repoName
    git clone $repoUrl $clonePath

    # Change to the repository directory
    Push-Location $clonePath

    # Fetch all branches
    git fetch --all

    # Get merged branches
    $mergedBranches = git branch -r --merged master | %{ $_.Trim() }

    # Identify stale branches (example: branches not updated in the last 30 days)
    $staleBranches = git for-each-ref --sort=committerdate refs/remotes/ --format='%(committerdate:short) %(refname)' | 
                     Where-Object { $_ -match '(\d{4}-\d{2}-\d{2})' -and (Get-Date $Matches[1]) -lt (Get-Date).AddDays(-30) }

    # Output the branches
    Write-Host "Repository: $repoName"
    Write-Host "Merged Branches:"
    $mergedBranches
    Write-Host "Stale Branches (Last commit > 30 days):"
    $staleBranches

    # Return to the original directory and delete the temporary clone
    Pop-Location
    Remove-Item $clonePath -Recurse -Force
}

# Example: Listing repositories (this part may vary based on how you access your BitBucket server repositories)
# For each repository URL, call the Get-StaleAndMergedBranches function
$repositoryUrls = @("https://example.com/repo1.git", "https://example.com/repo2.git") # Replace with actual repository URLs
foreach ($url in $repositoryUrls) {
    Get-StaleAndMergedBranches $url
}
