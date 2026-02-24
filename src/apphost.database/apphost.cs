#:sdk Aspire.AppHost.Sdk@13.3.0-preview.1.26121.1
#:property UserSecretsId=270f7f5f-f938-4fde-be8f-247819628151
#:package Aspire.Hosting.Azure.PostgreSQL@13.3.0-preview.1.26123.9

var builder = DistributedApplication.CreateBuilder(args);

var postgresAdminUsername = builder.AddParameter("mosaic-postgres-admin-username", secret: true);
var postgresAdminPassword = builder.AddParameter("mosaic-postgres-admin-password", secret: true);

var postgres = builder
    .AddAzurePostgresFlexibleServer("mosaic-postgres")
    .WithPasswordAuthentication(postgresAdminUsername, postgresAdminPassword);

postgres.AddDatabase("mosaicmoneydb");

builder.Build().Run();
