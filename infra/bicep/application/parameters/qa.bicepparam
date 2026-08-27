using '../main.bicep'

param location = 'centralindia'
param environment = 'qa'
param namePrefix = 'cpp-order'
param appServiceSku = 'B1'
param apiImageTag = 'latest'
param webImageTag = 'latest'
param mockJdeFailSubmissions = false
param logRetentionDays = 30
param tags = {}
