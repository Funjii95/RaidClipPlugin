$ErrorActionPreference='Stop'
. "$PSScriptRoot/Test-DeploymentConfiguration.ps1"
function Service($kind){
 $values=if($kind -eq 'backend'){@{'Actions__Enabled'='true';'Actions__PrivacyDeletionEnabled'='true';'Twitch__ChannelId'='42';'Data__ProjectId'='synthetic';'Data__DatabaseId'='(default)';'Data__Collection'='profiles';'Actions__AutomaticPlayerEnrollment'='true'}}else{@{'Publisher__WritesEnabled'='true';'Publisher__PrivacyDeletionEnabled'='true';'Publisher__ChannelId'='42';'Publisher__ProjectId'='synthetic';'Publisher__DatabaseId'='(default)';'Publisher__Collection'='profiles';'Publisher__AutomaticPlayerEnrollment'='true'}}
 @{template=@{containers=@(@{env=@($values.GetEnumerator()|ForEach-Object {@{name=$_.Key;value=$_.Value}})})}}
}
Test-RpgDeploymentConfiguration -Backend (Service backend) -Publisher (Service publisher)
$cases=@(@('backend','Actions__PrivacyDeletionEnabled','false'),@('publisher','Publisher__PrivacyDeletionEnabled','false'),@('backend','Actions__PrivacyDeletionEnabled',$null),@('publisher','Publisher__ChannelId','99'),@('backend','Data__Collection','other'),@('publisher','Publisher__AutomaticPlayerEnrollment','false'))
foreach($case in $cases){
 $b=Service backend;$p=Service publisher;$target=if($case[0] -eq 'backend'){$b}else{$p}
 ($target.template.containers[0].env|Where-Object name -eq $case[1]).value=$case[2]
 $rejected=$false;try{Test-RpgDeploymentConfiguration -Backend $b -Publisher $p|Out-Null}catch{$rejected=$true}
 if(!$rejected){throw 'Unsichere Konfiguration wurde nicht abgelehnt.'}
}
'PASS: 7 Deployment-Konfigurationstests.'
