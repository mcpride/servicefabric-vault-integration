# Project: servicefabric-vault-integration

## Description

Integrates Hashicorp's Vault into Microsoft Service Fabric, e.g. for on-prem scenarios.

## Hint

This project is just an very basic example - it works - but *IT IS NOT READY FOR PRODUCTION USE*! No waranties! Sorry for this, but I don't have much spare time.

## Getting Started

### Requirements

1. Microsoft Visual Studio 2019 or higher
1. Microsoft Azure ServiceFabric SDK installed
1. A ServiceFabric cluster running (Cloud, 1-Node or 5-Node)
1. HashiCorp's vault tool (Windows: vault.exe)

### Build

1. Copy the vault tool (vault.exe) into the 'VaultService' project directory.
1. Open the solution with Visual Studio
1. Ensure platform is set to x64 
1. Restore all NuGet packages
1. Build solution
1. Run unit tests
1. Deploy project to ServiceFabric cluster

## Details

### Why Microsoft Service Fabric?

If you have project requirements like high availability, scalability, independency (development, build, deployment) etc. then the microservice approach might be a solution.
There are a lot of orchestrators for microservices but most of them are more or less strongly tied to linux as os and docker/ kubernetes as platform. 
But what if cloud deployment is just an option and on-premises deployability is required but your customer is not prepared for a linux and docker infrastructure? 
Here comes the outsider Microsoft Service Fabric into play which can be hosted on Linux or Windows, in the (Azure) cloud or locally and can handle docker services but can also manage pure processes.   

### Why HashiCorp's Vault

Azure ServiceFabric has a rich tooling for cloud scenarios but just poor support for on-premises deployments - e.g. the key manager Azure KeyVault isn't available there. The independent tool "Vault" - available for diverse platforms - can fill such gaps because it is the "swiss army-knife" (in german: "eierlegende Wollmilchsau" ) for configuration, secrets and key management.
Vault has a lot of storage providers - some of them are enabled fo HA. Some of them are lesser stable e.g. the mssql provider makes heavy usage of inefficient "like" based search queries.
Service Fabric manages it's own strategies for high availability and statefulness which doesn't integrate very well with vault's possibilities.

### The challenge: Use vault with Service Fabric's HA features

This project provides a Service Fabric stateful service with one named partition, which configures, starts, stops and monitors the vault tool as an external process. It also provides a partial AWS S3 web interface, which will be configured as vaults storage stanza. The service then stores the received encrypted values from vault into Service Fabric's reliable dictionaries and also handles queries and deletions over it.

## ToDos

* Extend unit tests
* Add integration tests
* Implement consistent error handling
* Extend documentation
* Add build script's
* Add automated builds
* Add linux compatibility
* Improve security
  * SSL/TLS encryption
  * Manage authentication / authorizaion for local S3 web interface
* Auto-initialize, unseal and bootstrap vault
* Refactor code (e.g. use options for configuration etc.)

## Credits

This project uses some ideas and source code from [Gokhan Demir's (yadazula) S3Emulator project](https://github.com/yadazula/S3Emulator.git)

## License

The source code of this repository is under MIT license. See the [LICENSE](LICENSE) file for details. 
