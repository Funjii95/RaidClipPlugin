function Test-RpgDeploymentConfiguration {
 [CmdletBinding()]
 param([Parameter(Mandatory)]$Backend,[Parameter(Mandatory)]$Publisher)
 function Settings($service){
  $result=@{};foreach($entry in $service.template.containers[0].env){
   if($result.ContainsKey($entry.name)){throw 'Deployment gesperrt: doppelte Konfigurationsfelder.'}
   $result[$entry.name]=$entry.value
  };return $result
 }
 $b=Settings $Backend;$p=Settings $Publisher
 foreach($pair in @(@($b,'Actions__Enabled'),@($b,'Actions__PrivacyDeletionEnabled'),@($p,'Publisher__WritesEnabled'),@($p,'Publisher__PrivacyDeletionEnabled'))){
  if($pair[0][$pair[1]] -ne 'true'){throw "Deployment gesperrt: $($pair[1]) muss true sein. Offene Löschaufträge dürfen nicht durch fehlende Serverfreigabe hängen bleiben."}
 }
 if(!$b['Twitch__ChannelId'] -or $b['Twitch__ChannelId'] -ne $p['Publisher__ChannelId']){throw 'Deployment gesperrt: Kanalbindung stimmt nicht überein.'}
 foreach($key in @('ProjectId','DatabaseId','Collection')){
  if(!$b["Data__$key"] -or $b["Data__$key"] -ne $p["Publisher__$key"]){throw "Deployment gesperrt: Datenziel $key stimmt nicht überein."}
 }
 if($b['Actions__AutomaticPlayerEnrollment'] -ne $p['Publisher__AutomaticPlayerEnrollment']){throw 'Deployment gesperrt: Aufnahmefreigaben stimmen nicht überein.'}
 # Do not print credentials, entire service objects or configuration values.
 Write-Output 'PASS: Kanal, Datenziel, Aufnahme und geschützter Löschabschluss stimmen überein.'
}
