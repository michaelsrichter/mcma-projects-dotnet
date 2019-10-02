variable private_encryption_key {}

#########################
# Environment Variables
#########################

variable "environment_name" {}
variable "environment_type" {}
variable "global_prefix" {}
variable "global_prefix_lower_only" {}


#########################
# Azure Variables
#########################

variable "azure_client_id" {}
variable "azure_client_secret" {}
variable "azure_tenant_id" {}
variable "azure_tenant_name" {}
variable "azure_subscription_id" {}
variable "azure_location" {}


#########################
# Storage Variables
#########################

variable "deploy_container" {}
variable "upload_container" {}
variable "temp_container" {}
variable "repository_container" {}
variable "website_container" {}


#########################
# Azure VideoIndexer Variables
#########################

variable "azure_videoindexer_location" {}
variable "azure_videoindexer_account_id" {}
variable "azure_videoindexer_subscription_key" {}
variable "azure_videoindexer_api_url" {}