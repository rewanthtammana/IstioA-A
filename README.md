
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
After the services deployment, all micro services has an access to each other. This is not desirable because, logically, the account and the customers microservices does not need to call transfer microservice. This is not secure and not efficient. 

Out of the box, Istio cannot determine the access each service needs, so by default, it configures every service proxy to know about every other workload within the mesh which is not efficient. This bloats the configuration of the proxies needlessly. 

If we check the config of the Account service, there are records related to the Transfers service. This record doesn't need to stay in the account proxy config

```sh
  istioctl pc clusters deploy/accounts-accounts-api -n banking
```

![N|Solid](https://github.com/turkelk/IstioA-A/blob/main/Assets/accountscfgcluster.png)

```sh
  istioctl pc endpoints deploy/accounts-accounts-api -n banking
```

![N|Solid](https://github.com/turkelk/IstioA-A/blob/main/Assets/accountsendpoints.png)



Lets calculate the configuration size of the Accounts API workload:

```sh
  kubectl -n banking exec -ti svc/accounts-accounts-api -c accounts-api -- curl -s localhost:15000/config_dump > /tmp/config_dump
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

After default sidecar resource is created, Transfer APIs can not access Accounts and Customers API. 

```sh 
  kubectl exec svc/transfers-transfers-api -c transfers-api curl -I 'http://customers-customers-api.banking.svc.cluster.local/api/v1/customers?cif=1' -n banking
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
      - "./customers-customers-api.banking.svc.cluster.local"
      - "./accounts-accounts-api.banking.svc.cluster.local"
```

```sh 
  kubectl apply -f deployment/k8s/helm/transfers-api/chart/templates/_ist_egress.yaml
```

After transfers sidecar resource is created, Transfer APIs will able to call Customers and Accounts 

```sh 
  kubectl exec svc/transfers-transfers-api -c transfers-api curl -I 'http://customers-customers-api.banking.svc.cluster.local/api/v1/customers?cif=1' -n banking
```

![N|Solid](https://github.com/turkelk/IstioA-A/blob/main/Assets/transferscustomersucess.png)


So far so good, BUT we still have some issues. Remember Requirement. "Transfers API can call only GET customers from Customers API and GET accounts from Accounts API. ". As of now Transfers API can call any endpoint from both micro services. We can define policies on Customers API and Accounts API to make sure Transfers API can call only GET endpoint of customers and accounts resoruce.  

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
  kubectl exec svc/transfers-transfers-api -c transfers-api curl -I 'http://customers-customers-api.banking.svc.cluster.local/api/v1/customers?cif=1' -n banking
```

![N|Solid](https://github.com/turkelk/IstioA-A/blob/main/Assets/transferscustomersucess.png)

In order to enable Transfers API to access Customers API and Accounts API, a new AuthorizationPolicy must be applied to worklodes respectivelly.

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


There are multiple options to allow Transfers API to query the customers endpoint. 

  1. Service account can be used. We can create new AuthorizationPolicy which grants Transfers API Service Account to query customers resource of Customers API and to query accounts resource of Accounts API.

  2. Subject Claim(sub). We can create new AuthorizationPolicy which grants Transfers API`s client id (sub claim in JWT token) to query customers resource of Customers API and to query accounts resource of Accounts API.

  3. Scopes. Instead of just allowing sub claim (Clinet ID of Transfers API), allowing scopes.

Best option is 3rd one.  If we only allow Transfer API, AuthorizationPolicy will need to be changed in the future when there is another service that wants to access customers and accounts resources. It is against SOLID. Remeber O principle of SOLID. Open to Extension Close to Modification. But now anyone with customers_read privilege can query customers. AuthorizationPolicy will be written once and will never change but is open to expansion.


Let's cut it short and add the necessary policy to the Customers API workloads.

![N|Solid](https://github.com/turkelk/IstioA-A/blob/main/Assets/customersap.png)



```sh 
  kubectl apply -f deployment/k8s/helm/customers-api/chart/templates/_ist_customers_policy.yaml
```

After this policy is created, no one can access the customers resource that does not have a JWT token anymore. For GET operation, JWT must have read_only or full_access privileges.



<!-- We can define a AuthorizationPolicy that authorizes the 
  
  1. Use of Service Account: Transfers API`s service account to perform the GET operation on the customers and accounts resources.
  2. Use of JWT: JWT token`s sub (oauth 2.0 standart subject claim) to perform the GET operation on the customers and accounts resources.
  3.  -->

That topic is out of scope, I will focus on its implementation with JWT token.



We will consider B here. Client authentication becuase it is service(server) to service(servcer communication).  

We can 


When a Sidecar resource applies to a workload the control plane uses the egress field to determine to which services the workload requires access. That enables Istio’s control plane to discern relevant configuration and updates and send only those to the respective proxies. As a result, it avoids generating and distributing all the configurations on how to reach every other services and reduces the consumption of CPU, memory, and network bandwidth.

### DEFINING BETTER DEFAULTS WITH A MESH-WIDE SIDECAR CONFIGURATION

The easiest way to reduce the Envoy configuration sent to every service proxy and improve control performance is to define a mesh-wide Sidecar configuration that permits egress traffic only to services within the istio-system namespace. Defining such a default configures all proxies within the mesh with the minimal configuration to connect only to the control plane and drops all configuration for connectivity to other services.
This nudges service owners in the correct path of defining more specific Sidecar definitions for their workloads and explicitly state all egress traffic their services require. Thus ensuring that workloads receive minimal and relevant configuration needed for their processes.

With the Sidecar definition below we create a mesh-wide Sidecar definition that allows connectivity only to the Istio services located in the istio-system namespace.

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

- The sidecar in the **istio-system** namespace applies to the entire mesh
- Egress traffic is configured only for workloads in the istio-system namespace
- The **REGISTRY_ONLY** mode allows outbound traffic only to services configured by the Sidecar

```sh
 kubectl apply -f /istio/sidecar-mesh-wide.yaml
```

### Querying proxy configuration using istioctl

The istioctl proxy-config command enables us to retrieve and filter the proxy configuration of a workload based on the Envoy xDS APIs, where each subcommand is appropriately named:


- **cluster** - Retrieves cluster configuration 
- **endpoint** - Retrieves endpoint configuration 
- **listener** - Retrieves listener configuration 
- **route** - Retrieves route configuration
- **secret** - Retrieves secret configuration


```sh
istioctl pc clusters deploy/accounts-accounts-api -n banking
istioctl pc endpoints deploy/accounts-accounts-api -n banking
istioctl pc listeners deploy/accounts-accounts-api -n banking
istioctl pc routes deploy/accounts-accounts-api -n banking
```

### Egress Rules

Egress field specifies the configuration of the sidecar for processing outbound traffic from the attached workload instance to other services in the mesh.


Install curl on accounts container.

```sh
kubectl exec svc/accounts-accounts-api -c accounts-api  apk add curl -n banking
```

Let's make sure that the account api cannot access the customer api

```sh
kubectl exec svc/accounts-accounts-api -c accounts-api curl 'http://customers-customers-api.banking.svc.cluster.local/api/v1/customers?cif=1' -n banking
```

Let's allow the account api to access the customer api

```sh
apiVersion: networking.istio.io/v1beta1
kind: Sidecar
metadata:
  name: account
  namespace: banking
spec:
  workloadSelector:
    labels:
      app.kubernetes.io/instance: accounts
      app.kubernetes.io/name: accounts-api
  egress:
  - hosts:
    - "./customers-customers-api.banking.svc.cluster.local"
```

```sh
kubectl apply -f deployment/k8s/helm/accounts-api/chart/templates/_ist_sidecar_allow.yaml
```

Let's make sure that the account api can access the customer api

Run curl command on account to get customer by id

```sh
kubectl exec svc/accounts-accounts-api -c accounts-api curl 'http://customers-customers-api.banking.svc.cluster.local/api/v1/customers?cif=1' -n banking
```

### Restrict accounts-api to call only GET endpoints on customer-api

To increase security, and simplify our thought process, let’s define a mesh-wide policy that denies all requests that do not explicitly specify an ALLOW policy

```sh
apiVersion: security.istio.io/v1beta1
kind: AuthorizationPolicy
metadata:
 name: deny-all
 namespace: istio-system
spec: {}
```


```sh
 kubectl apply -f /istio/policy-deny-all-mesh.yaml
```

After the mesh wide deny all policy is created, let's check that the account api cannot access the customer api

Below command needs to print access denied

```sh
kubectl exec svc/accounts-accounts-api -c accounts-api curl 'http://customers-customers-api.banking.svc.cluster.local/api/v1/customers?cif=1' -n banking
```


Lets allow accounts-api to call customers-api with GET operation only

Create AuthorizationPolicy under customers ms with name _ist_authorization_policy.yaml

```sh
apiVersion: "security.istio.io/v1beta1"
kind: "AuthorizationPolicy"
metadata:
 name: "accounts-viewer"
 namespace: banking
spec:
 selector:
   matchLabels:
      app.kubernetes.io/instance: customers
      app.kubernetes.io/name: customers-api
 rules:
 - from:
   - source:
       principals: ["cluster.local/ns/banking/sa/accounts-accounts-api"]
   to:
   - operation:
       methods: ["GET"]
```

Create policy

```sh
 kubectl apply -f /istio/policy-deny-all-mesh.yaml
```

Let's make sure that the account api can call GET endpoint of customer api


```sh
kubectl exec svc/accounts-accounts-api -c accounts-api curl 'http://customers-customers-api.banking.svc.cluster.local/api/v1/customers?cif=1' -n banking
```

Let's try to make a POST call to customer api

Below call needs to print access denied.

```sh
kubectl exec -it svc/accounts-accounts-api -c accounts-api  bash -n banking
curl -X POST -H "Content-Type: application/json" -d '{"cif","1234"}' http://customers-customers-api.banking.svc/api/v1/customers -n banking
```

