using '../main.bicep'

param location = 'centralindia'
param environment = 'prod'
param namePrefix = 'cpp-order'
param appServiceSku = 'P1v3'
param apiImageTag = 'latest'
param webImageTag = 'latest'
param mockJdeFailSubmissions = false
param logRetentionDays = 30
param tags = {}
