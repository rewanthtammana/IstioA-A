
## Motivation
Applications and services often require Authentication and Autherization. Solving A&A with istio. 

## Context and Problem

Applications and services often require Authentication and Autherization. A library, framework can be built and tightly integrated into the application to fulfill the requirements. 

### Problem

- Considering that companies often use more than one language with microservices, separate framework needs to be developed for each language.

- Maintaining a handful of libraries across a bunch of programming languages and frameworks requires a lot of discipline and is very hard to get right. The key here is ensuring all of the implementations are consistent and correct.

- Also means they are not well isolated, and an outage in one of these components can affect other components or the entire application. 

### Solution - Sidecar pattern

The sidecar pattern is a single-node pattern made up of two containers. The first is the application container. It contains the core logic for the application. In addition to the application container, there is a sidecar container. The role of the sidecar is to augment and improve the application container, often without the application container’s knowledge

![N|Solid](https://github.com/turkelk/IstioA-A/blob/main/Assets/sidecarms.png)

## Scenario

The solution will proceed by considering the fund transfer example in banking.

In the transfer process, simple the status of the sender and receiver accounts and status of the account holder customers is checked.

### Services

1. Transfers API
2. Accounts API
3. Custoemrs API


### Fund Transfer Flow

![N|Solid](https://github.com/turkelk/IstioA-A/blob/main/Assets/trasferflow.png)

Simply, the fund transfer process in banking takes place as follows

- Transfer API makes call to Accounts API to get sender account details to verify it
- Transfer API makes call to Accounts API to get receiver account details to verify it
- Transfer API makes call to Customers API to get sender account holders(customer) details to verify it
- Transfer API makes call to Customers API to get receiver account holders(customer) details to verify it
- After the verification is complete, the process will take place

### Requirements

- Transers API should have an access to accounts resource in Accounts API and customers resource in Customers API with read-only (GET)scope. It should be able to get accounts and customers BUT it should not be able to create, update or delete them

## Pre Requirments

- k8s cluster - minikube

## Implementation

### Deploy Istio

```sh 
  sh deployment/k8s/deploy-istio.sh
```


### Create banking namespace and enable istio-injection for namespace

```sh 
  sh deployment/k8s/create-ns.sh
```

### Deploy the applicatons

```sh
  sh deployment/k8s/accounts.sh
  sh deployment/k8s/customers.sh
  sh deployment/k8s/transfers.sh
```
After the deployment, all micro services has an access to each other. This is not desirable because, logically, the account and the customers microservices does not need to call transfer microservice. This is not secure and not efficient. 

Out of the box, Istio cannot determine the access each service needs, so by default, it configures every service proxy to know about every other workload within the mesh which is not efficient. This bloats the configuration of the proxies needlessly. 

If we check the config of the Account service, there are records related to the Transfers service. This record doesn't need to stay in the account proxy config

```sh
  istioctl pc clusters deploy/accounts-apis-common -n banking
```

![N|Solid](https://github.com/turkelk/IstioA-A/blob/main/Assets/accountscfgcluster.png)

```sh
  istioctl pc endpoints deploy/accounts-apis-common -n banking
```

![N|Solid](https://github.com/turkelk/IstioA-A/blob/main/Assets/accountsendpoints.png)



Lets calculate the configuration size of the Accounts API workload:

```sh
  kubectl -n banking exec -ti svc/accounts-apis-common -c apis-common -- curl -s localhost:15000/config_dump > /tmp/config_dump
  du -sh /tmp/config_dump
  324K    /tmp/config_dump
```

So right now we have a configuration size of 324K. Considering that there are only 3 services, this value is huge.


### Fine tuning the configuration of inbound and outbound traffic for the sidecar proxies

To resolve these concerns, we can use the Sidecar resource that enables fine tuning the configuration of inbound and outbound traffic for the sidecar proxies.

The easiest way to reduce the Envoy configuration sent to every service proxy and improve control performance is to define a mesh-wide Sidecar configuration that permits egress traffic only to services within the istio-system namespace. Defining such a default configures all proxies within the mesh with the minimal configuration to connect only to the control plane and drops all configuration for connectivity to other services.
This nudges service owners in the correct path of defining more specific Sidecar definitions for their workloads and explicitly state all egress traffic their services require. Thus ensuring that workloads receive minimal and relevant configuration needed for their processes.

```sh 
apiVersion: networking.istio.io/v1beta1
kind: Sidecar
metadata:
  name: default
  namespace: istio-system
spec:
  egress:
  - hosts:
    - "istio-system/*"
  outboundTrafficPolicy:
    mode: REGISTRY_ONLY
```


```sh 
  sh deployment/k8s/egress-traffic-control-plane-only-mesh.sh
```

After default sidecar resource is created, Transfer APIs can not access Accounts and Customers API. We need to fix it because for transfer flow Transfer API should query customers and accounts resources. 

```sh 
  kubectl exec svc/transfers-apis-common -c apis-common sh -n banking
  curl 'http://customers-apis-common.banking.svc.cluster.local/api/v1/customers?cif=1'
```
![N|Solid](https://github.com/turkelk/IstioA-A/blob/main/Assets/transfers502badgateway.png)


We need to create specific sidecar resoruce for Transfers API worloads including Customer and Accounts API in egress rules.

```sh 
  apiVersion: networking.istio.io/v1beta1
  kind: Sidecar
  metadata:
    name: transfers
    namespace: banking
  spec:
    workloadSelector:
      labels:
        app.kubernetes.io/instance: transfers
        app.kubernetes.io/name: transfers-api
    egress:
    - hosts:
      - "./customers-apis-common.banking.svc.cluster.local"
      - "./accounts-apis-common.banking.svc.cluster.local"
```

```sh 
  kubectl apply -f deployment/k8s/helm/transfers-api/templates/_ist_egress.yaml
```

After transfers sidecar resource is created, Transfer APIs will able to query Customers and Accounts 

```sh 
  kubectl exec svc/transfers-apis-common -c apis-common sh -n banking 
  curl 'http://customers-apis-common.banking.svc.cluster.local/api/v1/customers?cif=1'
```

![N|Solid](https://github.com/turkelk/IstioA-A/blob/main/Assets/transferscustomersucess.png)


So far so good, BUT we still have some issues. Remember Requirement. "Transfers API can call only GET customers from Customers API and GET accounts from Accounts API. ". As of now Transfers API can call any endpoint from both micro services. We should define policies on Customers API and Accounts API to make sure Transfers API can call only GET endpoint of customers and accounts resoruce.  

## Creating mesh-wide policy that denies all requests that do not explicitly specify an ALLOW policy.

AuthorizationPolicy can be use to activate allow or deny the connection according to the action property. With the AuthorizationPolicy definition below we create a mesh wide policy that denies all requests(including Transfers API to Customers or Accounts API calls) that do not explicitly specify and ALLOW policy . After applying deny-all policy, all calls from any API to other API will get 403 Forbiden error.

```sh
apiVersion: security.istio.io/v1beta1
kind: AuthorizationPolicy
metadata:
 name: deny-all
 namespace: istio-system
spec: {}
```

```sh 
  kubectl apply -f deployment/istio/policy-deny-all-mesh.yaml
```

After deny-all policy is applied, calls should start getting 403 forbidden - RBAC: access denied

```sh 
  kubectl exec -it svc/transfers-apis-common -c apis-common sh -n banking 
  curl 'http://customers-apis-common.banking.svc.cluster.local/api/v1/customers?cif=1'
```

![N|Solid](https://github.com/turkelk/IstioA-A/blob/main/Assets/transferscustomersucess.png)

In order to enable Transfers API to access Customers API and Accounts API, a new AuthorizationPolicy must be applied to Customers and Accounts  worklodes respectivelly.

Before going into the details of AuthorizationPolicy and implementation, it is useful to talk about the JWT token.

### JSON Web Token (JWT) Profile

JSON Web Token (JWT) [JWT] is a JSON-based [RFC7159] security token encoding that enables identity and security information to be shared across security domains.  A security token is generally issued by an Identity Provider and consumed by a Relying Party that relies on its content to identify the token's subject for security-related purposes.


- "sub" (subject): The JWT MUST contain a sub claim identifying the principal that is the subject of the JWT.  Two cases need to be differentiated:

        A.  For the authorization grant, the subject typically
            identifies an authorized accessor for which the access token
            is being requested (i.e., the resource owner or an
            authorized delegate), but in some cases, may be a
            pseudonymous identifier or other value denoting an anonymous
            user.

        B.  For client authentication, the subject MUST be the
            "client_id" of the OAuth client.

- scopes: Scopes are groups of claims.

Scopes are often described as a mechanism to limit the access of the requesting party to the user’s resources. The client can request scope customers_read, meaning that the issued token will allow it to only query the customers endpoints and not to make any changes.


There are multiple options to allow Transfers API to query the customers and accounts endpoint. 

  1. Service account can be used. We can create new AuthorizationPolicy which grants Transfers API Service Account to query customers resource of Customers API and to query accounts resource of Accounts API.

  2. Subject Claim(sub). We can create new AuthorizationPolicy which grants Transfers API`s client id (sub claim in JWT token) to query customers resource of Customers API and to query accounts resource of Accounts API.

  3. Scopes. Instead of just allowing sub claim (Clinet ID of Transfers API) or Service account, allowing scopes.

Best option is 3rd one. If we only authorize the service account or clientId(sub) of the Transfer API, it will be necessary to edit the AuthorizationPolicy when other services want to access the same resources in the future. It is against SOLID. Remeber O principle of SOLID. Open to Extension Close to Modification. Authorizing a scope instead of a service account or sub will provide access to the customers and accounts resources for anyone with required scopes in JWT. AuthorizationPolicy will be written once and will never change but is open to expansion.


Let's cut it short and add the necessary policy to the Customers API workloads.

![N|Solid](https://github.com/turkelk/IstioA-A/blob/main/Assets/customersap.png)

```sh 
  kubectl apply -f deployment/k8s/helm/customers-api/templates/_ist_customers_policy.yaml
```

After this policy is created, no one can access the customers resource that does not have a JWT token anymore. JWT token is required and we need to acquire one. Alos For GET operation, JWT token must have at least one of the privileges in the list [customer:customers:read_only, customer:customers:full_access, customer:full_access]


### KEYCLOAK

KEYCLOAK is an OpenID Connect and OAuth 2.0 framework. It is a tool for “Identity and Access Management”. We will use keycloak to acquire and validate JWT tokens 

```sh 
  sh deployment/k8s/keycloak.sh
```


I will not explain the part of creating a client by logging in with keycloak admin user. We need to change some settings for client credentials flow in Keycloak

![N|Solid](https://github.com/turkelk/IstioA-A/blob/main/Assets/keycloakclient.png)


After the client is created, we can get access tokens by sending a request to the token endpoint.

client_secret in curl request can be found in Credentials tab under Clients

![N|Solid](https://github.com/turkelk/IstioA-A/blob/main/Assets/keycloakcred.png)


```sh 
curl --location --request POST 'http://127.0.0.1/auth/realms/master/protocol/openid-connect/token' \
--header 'Content-Type: application/x-www-form-urlencoded' \
--data-urlencode 'client_id=transfers-api' \
--data-urlencode 'client_secret=tGnNTLp0f7ySRLOgMnfHokmxwpDxh3bT' \
--data-urlencode 'grant_type=client_credentials'
```

Even if we get the access token obtained with Client Id and Secret and add as Bearer to the Authorization header, we will continue to receive the 403 Forbidden. 

```sh 
  kubectl exec -it svc/transfers-apis-common -c apis-common sh -n banking 
  curl -i http://customers-apis-common.banking.svc.cluster.local/api/v1/customers?cif=1 -H "Authorization: Bearer eyJhbGciOiJSUzI1NiIsInR5cCIgOiAiSldUIiwia2lkIiA6ICJLU2lXb21GcHNyLVNtT2ZDWnpuc1RRekd6UUtzaHJNcXFqT2FZV1FXWk5vIn0.eyJleHAiOjE2NDI1MDM1MTQsImlhdCI6MTY0MjUwMzQ1NCwianRpIjoiNGRmMWY0MjctYzMyOC00NzM4LTgwNGEtYWYxYjgzODNlZmI1IiwiaXNzIjoiaHR0cDovLzEyNy4wLjAuMS9hdXRoL3JlYWxtcy9tYXN0ZXIiLCJzdWIiOiI3YzNiOGQwZC1hMDAyLTQxYzktYWU5Mi0wMjZiYzNmMGU2YzkiLCJ0eXAiOiJCZWFyZXIiLCJhenAiOiJ0cmFuc2ZlcnMtYXBpIiwiYWNyIjoiMSIsInNjb3BlIjoiY3VzdG9tZXI6Y3VzdG9tZXJzOnJlYWRfb25seSIsImNsaWVudElkIjoidHJhbnNmZXJzLWFwaSIsImNsaWVudEhvc3QiOiIxNzIuMTcuMC4xIiwiY2xpZW50QWRkcmVzcyI6IjE3Mi4xNy4wLjEifQ.HGemTETggVikhV3GT8NgKKjyVNG67wNR9xbY8YWfo-BZK0Rg4X-w8Mvo62RLsKGKw7rcSr-WgQC0ttG21YHxbkNSb5tUHLwr-L28SxW7nTgXmNK7ye11IEBv6W8MGyYIFXLKuaJDBvi-Gc2oUffudsHJWt5Bjho_iD54wSuZ0YjURYVZUMTdTDjZC8OPuIrDdmPAeKhu1zombpcMKrCrTosYrAbA2hbQaQCcMVP4jJZcDjOQdlTYjT9Fe5mcqnxIuxc-O1-8jdEg0v8DHu1MbAzK386Vg5nsWFnR_rt2px41d6g2e0TKypraQIt-q7QcOsRyr4IlP0VecUkV_WzkNA"
```

There could be two reasons for this.

1. We need to understand how RequestAuthentication and AuthorizationPolicy work. As can be seen in the diagram below, after the request authenticated the data contained in the SVID and JWT Token are extracted and stored as metadata to be user by Authorization Filters.

![N|Solid](https://github.com/turkelk/IstioA-A/blob/main/Assets/istiosecuritynutshel.png)

Since we still haven't defined a RequestAuthentication, the filter metadata is null and the information in the JWT token is not populated in the context. So rules in AuthorizationPolicy can not be executed.


Seen in the diagram below reques.auth.claims is in the filter metadata and is extracted and populated from the JWT token during RequestAuthentication

![N|Solid](https://github.com/turkelk/IstioA-A/blob/main/Assets/policyrulescustomer.png)


Lets create RequestAuthentication for banking namespace.

```sh 
apiVersion: security.istio.io/v1beta1
kind: RequestAuthentication
metadata:
  name: master-realm
  namespace: banking
spec:
  jwtRules:
  - issuer: "http://127.0.0.1/auth/realms/master" 
    jwksUri: http://idp-keycloak.default.svc/auth/realms/master/protocol/openid-connect/certs    
```

![N|Solid](https://github.com/turkelk/IstioA-A/blob/main/Assets/reqauth.png)

```sh    
  kubectl apply -f deployment/istio/banking-RequestAuthentication.yaml  
```

After creatin RequestAuthentication we will still get the RBAC access denied - 403 Forbidden error.

2. Second reason 403 Forbidden - We keep getting this error because at required scopes defined in the AuthorizationPolicy for GET is missing in the JWT token as shown below.

![N|Solid](https://github.com/turkelk/IstioA-A/blob/main/Assets/custscopesap.png)

We need to create the required scopes (customer:customers:read_only - it is sufficient for transfer api because transfer api should call customers endpoint read only) in Keycloak admin console and assing those scopes to the transfes-api client.

![N|Solid](https://github.com/turkelk/IstioA-A/blob/main/Assets/keycloakcustomerscopes.png)

![N|Solid](https://github.com/turkelk/IstioA-A/blob/main/Assets/addscope.png)

Let's get access token again after definition of scope.

```sh 
curl --location --request POST 'http://127.0.0.1/auth/realms/master/protocol/openid-connect/token' \
--header 'Content-Type: application/x-www-form-urlencoded' \
--data-urlencode 'client_id=transfers-api' \
--data-urlencode 'client_secret=tGnNTLp0f7ySRLOgMnfHokmxwpDxh3bT' \
--data-urlencode 'grant_type=client_credentials'
```

Let's examine the token content. The scope we just added should be there.

![N|Solid](https://github.com/turkelk/IstioA-A/blob/main/Assets/jwtio.png)


Let's query customers and check if it is working wiht new JWT token.

BINGOOO

![N|Solid](https://github.com/turkelk/IstioA-A/blob/main/Assets/successwithjwt.png)


