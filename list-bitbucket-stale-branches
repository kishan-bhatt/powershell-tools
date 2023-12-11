# Define BitBucket Server details
$bitbucketServerUrl = "https://your-bitbucket-server"
$projectKey = "YOUR_PROJECT_KEY"
$repoSlug = "YOUR_REPO_SLUG"

# Basic Authentication (Update as per your auth method)
$user = "your-username"
$pass = "your-password"
$base64AuthInfo = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes(("$user:$pass")))

# Function to get branches from BitBucket
function Get-BitBucketBranches {
    param (
        [int]$daysOld = 60
    )

    # API Endpoint to list branches
    $apiUrl = "$bitbucketServerUrl/rest/api/1.0/projects/$projectKey/repos/$repoSlug/branches"

    # Call BitBucket API
    try {
        $branches = Invoke-RestMethod -Uri $apiUrl -Method Get -Headers @{Authorization=("Basic {0}" -f $base64AuthInfo)}
    }
    catch {
        Write-Error "Error fetching branches: $_"
        return
    }

    # Filter branches based on criteria
    $currentDate = Get-Date
    $branchesFiltered = $branches | Where-Object {
        # Logic to determine if a branch is stale, merged, or unmerged but no commits since X days
        # Placeholder logic - needs to be replaced with actual criteria
        $_.lastCommitDate -lt $currentDate.AddDays(-$daysOld) # Example criterion
    }

    return $branchesFiltered
}

# User input for days
$days = Read-Host -Prompt "Enter the number of days to check for stale branches (default is 60)"
if ([string]::IsNullOrWhiteSpace($days)) {
    $days = 60
}

# Fetch and filter branches
$filteredBranches = Get-BitBucketBranches -daysOld $days

# Export to CSV
$filteredBranches | Select-Object -Property name | Export-Csv -Path "branches.csv" -NoTypeInformation

Write-Host "Branches exported to branches.csv"
