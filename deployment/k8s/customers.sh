#!/bin/bash

DIR=./helm/customers-api
RELEASE=customers
NAMESPACE=banking

parent_path=$( cd "$(dirname "${BASH_SOURCE[0]}")" ; pwd -P )
cd "$parent_path"
sh api-common.sh --dir ${DIR} --release ${RELEASE} --namespace ${NAMESPACE}