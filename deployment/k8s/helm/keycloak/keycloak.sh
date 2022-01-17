#!/bin/bash

helm repo add bitnami https://charts.bitnami.com/bitnami

helm install keycloak \
--set auth.adminPassword=admin \
--set postgresql.postgresqlPassword=admin \
bitnami/keycloak