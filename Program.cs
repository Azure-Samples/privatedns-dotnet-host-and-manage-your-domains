// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager.Resources.Models;
using Azure.ResourceManager.Samples.Common;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager;
using System.Reflection;
using Azure.ResourceManager.PrivateDns;
using Azure.ResourceManager.Network;
using System.Threading;
using Azure.ResourceManager.Compute;
using Azure.ResourceManager.PrivateDns.Models;
using System.Net;
using Azure.ResourceManager.Compute.Models;
using System.Net.WebSockets;

namespace ManagePrivateDns
{
    public class Program
    {
        private static ResourceIdentifier? _resourceGroupId = null;
        private const string CustomDomainName = "private.contoso.com";

        /**
         * Azure private DNS sample for managing DNS zones.
         *  - Creates a private DNS zone (private.contoso.com)
         *  - Creates a virtual network
         *  - Link a virtual network
         *  - Creates test virtual machines
         *  - Creates an additional DNS record
         *  - Test the private DNS zone
         */
        public static async Task RunSample(ArmClient client)
        {

            {
                // Get default subscription
                SubscriptionResource subscription = await client.GetDefaultSubscriptionAsync();

                // Create a resource group in the EastUS region
                string rgName = Utilities.CreateRandomName("PrivateDnsTemplateRG");
                Utilities.Log($"creating resource group...");
                ArmOperation<ResourceGroupResource> rgLro = await subscription.GetResourceGroups().CreateOrUpdateAsync(WaitUntil.Completed, rgName, new ResourceGroupData(AzureLocation.EastUS));
                ResourceGroupResource resourceGroup = rgLro.Value;
                _resourceGroupId = resourceGroup.Id;
                Utilities.Log("Created a resource group with name: " + resourceGroup.Data.Name);

                //============================================================
                // Creates a private DNS zone

                Utilities.Log("Creating private DNS zone...");
                string zoneName = $"{Utilities.CreateRandomName("privateDnsZone")}.com";
                PrivateDnsZoneData zoneInput = new PrivateDnsZoneData("global")
                {
                    Tags =
                    {
                        new KeyValuePair<string, string>("key","value"),
                        new KeyValuePair<string, string>("key2","value")
                    }
                }; ;
                var privateDnsZoneLro = await resourceGroup.GetPrivateDnsZones().CreateOrUpdateAsync(WaitUntil.Completed, zoneName, zoneInput);
                PrivateDnsZoneResource privateDnsZone = privateDnsZoneLro.Value;
                Utilities.Log("Created private DNS zone " + privateDnsZone.Data.Name);

                //============================================================
                // Creates a virtual network

                string vnetName = Utilities.CreateRandomName("vnet");
                Utilities.Log("Creating virtual network...");
                VirtualNetworkData vnetInput = new VirtualNetworkData()
                {
                    Location = resourceGroup.Data.Location,
                    AddressPrefixes = { "10.10.0.0/16" },
                    Subnets =
                    {
                        new SubnetData() { Name = "default", AddressPrefix = "10.10.1.0/24" },
                        new SubnetData() { Name = "subnet1", AddressPrefix = "10.10.2.0/24" },
                        new SubnetData() { Name = "subnet2", AddressPrefix = "10.10.3.0/24" }
                    }
                };
                var vnetLro = await resourceGroup.GetVirtualNetworks().CreateOrUpdateAsync(WaitUntil.Completed, vnetName, vnetInput);
                VirtualNetworkResource vnet = vnetLro.Value;
                Utilities.Log("Created virtual network: " + vnet.Data.Name);

                //============================================================
                // Link a virtual network

                Utilities.Log("Creating virtual network link within private zone...");
                string linkName = Utilities.CreateRandomName("link");
                VirtualNetworkLinkData linkInput = new VirtualNetworkLinkData("global")
                {
                    RegistrationEnabled = true,
                    VirtualNetworkId = vnet.Id,
                };
                var link = await privateDnsZone.GetVirtualNetworkLinks().CreateOrUpdateAsync(WaitUntil.Completed, linkName, linkInput);
                Utilities.Log("Linked a virtual network " + vnet.Data.Name);

                //============================================================
                // To create VMs, pre-create two NICs.
                string publicIPName1 = Utilities.CreateRandomName("pip");
                ResourceIdentifier subnetId1 = (await vnet.GetSubnets().GetAsync("subnet1")).Value.Data.Id;
                ResourceIdentifier subnetId2 = (await vnet.GetSubnets().GetAsync("subnet2")).Value.Data.Id;
                NetworkInterfaceResource nic1 = await Utilities.CreateVirtualNetworkInterface(resourceGroup, subnetId1, publicIPName1);
                NetworkInterfaceResource nic2 = await Utilities.CreateVirtualNetworkInterface(resourceGroup, subnetId2);

                //============================================================
                // Creates test virtual machines
                Utilities.Log("Creating first virtual machine...");
                VirtualMachineResource vm1 = await Utilities.CreateVirtualMachine(resourceGroup, nic1.Data.Id, "vm001");
                Utilities.Log("Created first virtual machine " + vm1.Data.Name);

                Utilities.Log("Creating second virtual machine...");
                VirtualMachineResource vm2 = await Utilities.CreateVirtualMachine(resourceGroup, nic2.Data.Id, "vm002");
                Utilities.Log("Created second virtual machine " + vm2.Data.Name);

                //============================================================
                // Creates an additional DNS record
                Utilities.Log("Creating additional record set...");
                Utilities.Log("Get vm1 public IP..");
                var vm1PubliIP = await resourceGroup.GetPublicIPAddresses().GetAsync(publicIPName1);
                string vm1PubliIPString = vm1PubliIP.Value.Data.IPAddress;
                Utilities.Log($"vm1 public IP: {vm1PubliIPString}");

                string aRecordName = "vm001arecord";
                PrivateDnsARecordData aRecordInput = new PrivateDnsARecordData()
                {
                    TtlInSeconds = 3600,
                    PrivateDnsARecords =
                    {
                        new PrivateDnsARecordInfo()
                        {
                            IPv4Address = IPAddress.Parse("10.10.2.4")
                        }
                    }
                };
                var aRecordLro = await privateDnsZone.GetPrivateDnsARecords().CreateOrUpdateAsync(WaitUntil.Completed, aRecordName, aRecordInput);
                PrivateDnsARecordResource aRecord = aRecordLro.Value;
                Utilities.Log("Created additional record set " + aRecord.Data.Name);

                //============================================================
                // Test the private DNS zone

                Utilities.Log("Configure VMs to allow inbound ICMP");
                RunCommandInput initCommandInput = new RunCommandInput("RunPowerShellScript")
                {
                    Script = { "New-NetFirewallRule –DisplayName \"Allow ICMPv4-In\" –Protocol ICMPv4" }
                };
                _ = await vm1.RunCommandAsync(WaitUntil.Completed, initCommandInput);
                _ = await vm2.RunCommandAsync(WaitUntil.Completed, initCommandInput);

                Utilities.Log($"VM2 run command: ping vm001arecord.{privateDnsZone.Data.Name}");
                RunCommandInput pingCommandInput = new RunCommandInput("RunPowerShellScript")
                {
                    Script = { $"ping vm001arecord.{privateDnsZone.Data.Name}" }
                };
                var result = await vm1.RunCommandAsync(WaitUntil.Completed, pingCommandInput);
                Utilities.Log(result.Value.Value.First().Message);
            }
            //finally
            //{
            //    try
            //    {
            //        Utilities.Log("Deleting Resource Group: " + rgName);
            //        azure.ResourceGroups.DeleteByName(rgName);
            //        Utilities.Log("Deleted Resource Group: " + rgName);
            //    }
            //    catch (Exception)
            //    {
            //        Utilities.Log("Did not create any resources in Azure. No clean up is necessary");
            //    }
            //}
        }

        public static async Task Main(string[] args)
        {
            var clientId = Environment.GetEnvironmentVariable("CLIENT_ID");
            var clientSecret = Environment.GetEnvironmentVariable("CLIENT_SECRET");
            var tenantId = Environment.GetEnvironmentVariable("TENANT_ID");
            var subscription = Environment.GetEnvironmentVariable("SUBSCRIPTION_ID");
            ClientSecretCredential credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
            ArmClient client = new ArmClient(credential, subscription);

            await RunSample(client);
            try
            {
                //=================================================================
                // Authenticate
            }
            catch (Exception e)
            {
                Utilities.Log(e);
            }
        }
    }
}
