#!/bin/bash

kubectl create ns banking

kubectl label namespace banking istio-injection=enabled