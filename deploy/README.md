# Novus Website Deployment

## Prerequisites

- Kubernetes cluster with Traefik ingress
- `kubectl` configured with cluster access
- Docker for local builds
- Woodpecker CI (optional, for automated deployments)

## Manual Deployment

### 1. Build and push the image

```bash
docker build -f website/Dockerfile -t ghcr.io/barryw/novuslang-website:latest .
docker push ghcr.io/barryw/novuslang-website:latest
```

### 2. Deploy to Kubernetes

```bash
# First time - create namespace and all resources
kubectl apply -k deploy/

# Subsequent deploys - just update the image
kubectl set image deployment/novuslang-website \
  website=ghcr.io/barryw/novuslang-website:latest \
  -n novus
kubectl rollout status deployment/novuslang-website -n novus
```

### 3. Verify

```bash
kubectl get pods -n novus
kubectl get ingress -n novus
curl -I https://novuslang.com
```

## Woodpecker CI Setup

### Required Secrets

Configure these in Woodpecker:

| Secret | Description |
|--------|-------------|
| `docker_username` | GitHub username (`barryw`) |
| `ghcr_token` | GitHub PAT with `write:packages` scope |
| `kubeconfig` | Base64-encoded kubeconfig for cluster access |
| `s3_endpoint` | (Optional) S3 endpoint for guide artifacts |
| `s3_access_key` | (Optional) S3 access key |
| `s3_secret_key` | (Optional) S3 secret key |

### Generate kubeconfig secret

```bash
cat ~/.kube/config | base64 | tr -d '\n'
```

### Trigger initial deployment

After adding secrets, trigger a manual build with message containing "initial-deploy":

```bash
# Or use Woodpecker UI to trigger manual build
```

## Files

- `namespace.yaml` - Creates the `novus` namespace
- `website-deployment.yaml` - Website deployment (2 replicas)
- `website-service.yaml` - ClusterIP service
- `website-ingress.yaml` - Traefik ingress for novuslang.com
- `kustomization.yaml` - Kustomize configuration

## Scaling

```bash
kubectl scale deployment/novuslang-website --replicas=3 -n novus
```

## Troubleshooting

```bash
# Check pod logs
kubectl logs -l app=novuslang-website -n novus

# Check events
kubectl get events -n novus --sort-by='.lastTimestamp'

# Describe deployment
kubectl describe deployment novuslang-website -n novus
```

## Recovery Log

- **2026-07-01 (WAL-104):** novuslang.com was returning HTTP 502 — the `novus`
  namespace had no ready `novuslang-website` endpoints (service unreachable in-cluster,
  Traefik 502). Re-ran this website pipeline (kaniko rebuild → `kubectl apply -k deploy/`
  → `kubectl set image` → `rollout status` → Cloudflare cache purge) to restore the site.

- **2026-07-01 (WAL-104, follow-up):** deploy step now deletes the Deployment
  before `apply` — the live spec had a duplicate `http` container port that made
  `kubectl apply` reject the object, leaving zero ready pods.

- **2026-07-01 (WAL-104, hardening):** replaced the delete-before-apply (which
  briefly took the site down on *every* deploy) with a zero-downtime, fail-safe
  pattern: server-side `--dry-run` validation before any mutation, `kubectl apply
  --server-side --force-conflicts` (no client-side merge → no duplicate-port
  wedge), and automatic `kubectl rollout undo` if the new revision never becomes
  healthy. Combined with the Deployment's `maxUnavailable: 0` and a
  PodDisruptionBudget (`minAvailable: 1`), a bad manifest or image can no longer
  take novuslang.com offline.
