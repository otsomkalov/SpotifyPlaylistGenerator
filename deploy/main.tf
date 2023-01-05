terraform {
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = ">=3.37.0"
    }
  }
}

provider "azurerm" {
  features {}
}

locals {
  tags = {
    env  = var.env
    name = "spotify-playlist-generator"
  }
}

resource "azurerm_resource_group" "rg-spotify-playlist-generator" {
  name     = "rg-spotify-playlist-generator-${var.env}"
  location = "France Central"

  tags = local.tags
}

resource "azurerm_application_insights" "appi-spotify-playlist-generator" {
  resource_group_name = azurerm_resource_group.rg-spotify-playlist-generator.name
  location            = azurerm_resource_group.rg-spotify-playlist-generator.location

  name             = "appi-spotify-playlist-generator-${var.env}"
  application_type = "web"
}

resource "azurerm_storage_account" "st-spotify-playlist-generator" {
  resource_group_name = azurerm_resource_group.rg-spotify-playlist-generator.name
  location            = azurerm_resource_group.rg-spotify-playlist-generator.location

  name                     = "stspotifyplaylistgen${var.env}"
  account_tier             = "Standard"
  account_replication_type = "LRS"
  account_kind             = "Storage"

  tags = local.tags
}

resource "azurerm_service_plan" "asp-spotify-playlist-generator" {
  resource_group_name = azurerm_resource_group.rg-spotify-playlist-generator.name
  location            = azurerm_resource_group.rg-spotify-playlist-generator.location

  name     = "asp-spotify-playlist-generator-${var.env}"
  os_type  = "Linux"
  sku_name = "Y1"

  tags = local.tags
}

resource "azurerm_linux_function_app" "func-spotify-playlist-generator" {
  resource_group_name = azurerm_resource_group.rg-spotify-playlist-generator.name
  location            = azurerm_resource_group.rg-spotify-playlist-generator.location

  storage_account_name       = azurerm_storage_account.st-spotify-playlist-generator.name
  storage_account_access_key = azurerm_storage_account.st-spotify-playlist-generator.primary_access_key
  service_plan_id            = azurerm_service_plan.asp-spotify-playlist-generator.id

  name = "func-spotify-playlist-generator-${var.env}"

  functions_extension_version = "~4"

  site_config {
    application_insights_key = azurerm_application_insights.appi-spotify-playlist-generator.instrumentation_key
  }

  app_settings = {
    Telegram__Token       = var.telegram-token
    Telegram__BotUrl      = var.telegram-bot-url
    Spotify__ClientId     = var.spotify-client-id
    Spotify__ClientSecret = var.spotify-client-secret
    Spotify__CallbackUrl  = var.spotify-callback-url
    AWS_ACCESS_KEY_ID     = var.aws-access-key-id
    AWS_SECRET_ACCESS_KEY = var.aws-secret-access-key
    Amazon__QueueUrl      = var.amazon-queue-url
    GeneratorSchedule     = var.generator-schedule
  }

  connection_string {
    name  = "Postgre"
    type  = "PostgreSQL"
    value = var.database-connection-string
  }

  tags = local.tags
}
