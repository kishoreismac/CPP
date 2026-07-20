variable "subscription_id" {
  description = "Azure subscription in which resources are created."
  type        = string
}

variable "location" {
  description = "Azure region for all resources."
  type        = string
  default     = "centralindia"
}

variable "environment" {
  description = "Short environment name used in resource names and tags."
  type        = string
  default     = "dev"

  validation {
    condition     = contains(["dev", "test", "qa", "prod"], var.environment)
    error_message = "environment must be dev, test, qa, or prod."
  }
}

variable "name_prefix" {
  description = "Lowercase workload prefix."
  type        = string
  default     = "cpp-order"

  validation {
    condition     = length(var.name_prefix) <= 24 && can(regex("^[a-z][a-z0-9-]*[a-z0-9]$", var.name_prefix))
    error_message = "name_prefix must be at most 24 characters, start with a lowercase letter, and contain only lowercase letters, digits, and hyphens."
  }
}

variable "app_service_sku" {
  description = "Linux App Service Plan SKU. Use at least P1v3 for production."
  type        = string
  default     = "B1"
}

variable "api_image_tag" {
  description = "API image tag already pushed to the provisioned ACR."
  type        = string
  default     = "latest"
}

variable "web_image_tag" {
  description = "Web image tag already pushed to the provisioned ACR."
  type        = string
  default     = "latest"
}

variable "mock_jde_fail_submissions" {
  description = "When true, the demonstration fulfillment adapter rejects submissions."
  type        = bool
  default     = false
}

variable "log_retention_days" {
  description = "Log Analytics retention in days."
  type        = number
  default     = 30
}

variable "tags" {
  description = "Additional Azure tags."
  type        = map(string)
  default     = {}
}
