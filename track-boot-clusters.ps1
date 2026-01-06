# ============================
# CONFIG
# ============================

$start = (Get-Date).AddDays(-90)

# Expected boot events for your system
$expectedBootEvents = @(
    @{ Provider = 'Microsoft-Windows-Kernel-General'; Id = 12 },
    @{ Provider = 'Microsoft-Windows-Wininit'; Id = 12 },
    @{ Provider = 'Microsoft-Windows-Security-Auditing'; Id = 4608 },
    @{ Provider = 'EventLog'; Id = 6005 }
)


# ============================
# COLLECT BOOT EVENTS
# ============================

$rawBoots = @()

# Kernel-General 12
$rawBoots += Get-WinEvent -FilterHashtable @{
    LogName = 'System'; Id = 12; ProviderName = 'Microsoft-Windows-Kernel-General'; StartTime = $start
} -ErrorAction SilentlyContinue | Select TimeCreated, Id, ProviderName

# Wininit 12
$rawBoots += Get-WinEvent -FilterHashtable @{
    LogName = 'System'; Id = 12; ProviderName = 'Microsoft-Windows-Wininit'; StartTime = $start
} -ErrorAction SilentlyContinue | Select TimeCreated, Id, ProviderName

# EventLog 6005
$rawBoots += Get-WinEvent -FilterHashtable @{
    LogName = 'System'; Id = 6005; StartTime = $start
} -ErrorAction SilentlyContinue | Select TimeCreated, Id, ProviderName

# Security 4608
$rawBoots += Get-WinEvent -FilterHashtable @{
    LogName = 'Security'; Id = 4608; StartTime = $start
} -ErrorAction SilentlyContinue | Select TimeCreated, Id, ProviderName

# Sort all boot events
$rawBoots = $rawBoots | Sort-Object TimeCreated


# ============================
# CLUSTER BOOT EVENTS
# ============================

$clusters = @()
$currentCluster = @()

foreach ($evt in $rawBoots) {

    if ($currentCluster.Count -eq 0) {
        $currentCluster += $evt
        continue
    }

    $last = $currentCluster[-1]

    # Same cluster if within 10 seconds
    if (($evt.TimeCreated - $last.TimeCreated).TotalSeconds -le 10) {
        $currentCluster += $evt
    }
    else {
        $clusters += , @($currentCluster)
        $currentCluster = @($evt)
    }
}

if ($currentCluster.Count -gt 0) {
    $clusters += , @($currentCluster)
}


# ============================
# BUILD CLUSTER OBJECTS
# ============================

$boots = foreach ($cluster in $clusters) {

    $clusterEvents = $cluster | Select ProviderName, Id, TimeCreated

    # Determine missing events
    $missing = foreach ($exp in $expectedBootEvents) {
        $found = $clusterEvents | Where-Object {
            $_.ProviderName -eq $exp.Provider -and $_.Id -eq $exp.Id
        }
        if (-not $found) {
            "$($exp.Provider) ID $($exp.Id)"
        }
    }

    # Readable event summary
    $eventSummary = ($clusterEvents |
        Sort-Object TimeCreated |
        ForEach-Object {
            "{0} {1} @ {2}" -f `
            ($_.ProviderName -replace 'Microsoft-Windows-', ''), `
                $_.Id, `
                $_.TimeCreated.ToString("HH:mm:ss")
        }) -join "; "

    [PSCustomObject]@{
        BootTime      = ($cluster | Select-Object -First 1).TimeCreated.ToString("yyyy-MM-dd HH:mm:ss")
        EventSummary  = $eventSummary
        MissingEvents = $missing -join ", "
    }
}

$boots | Sort-Object BootTime
