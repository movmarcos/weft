# Octopus step templates

Two templates ship here. Import each once into Octopus Deploy (Library → Step templates → Import).

| File | Step template name | Use |
|---|---|---|
| `weft-deploy.json` | `Weft — Deploy Power BI Model` | Full diff-based deploy |
| `weft-refresh.json` | `Weft — Refresh Power BI Model` | Targeted refresh of listed tables |

## Typical Octopus project structure

```
Project: PowerBI-Sales-Model
  Variables:
    TenantId                (project, sensitive)
    SpClientId              (project, sensitive, scope: dev/uat/prod)
    CertThumbprint          (project, sensitive, scope: dev/uat/prod)
    WorkspaceUrl            (scoped per environment)
    DatabaseName            (scoped per environment)
    WeftParam.DatabaseName  (scoped per environment → WEFT_PARAM_DatabaseName)
    WeftParam.ServerName    (scoped per environment)

  Process:
    Step 1: Deploy a package (the weft-*.zip artifact + your model)
    Step 2: Weft — Deploy Power BI Model
      - Config File: weft.yaml
      - Target Profile: #{Octopus.Environment.Name}
      - SP Client Id: #{SpClientId}
      - Tenant Id: #{TenantId}
      - Cert Thumbprint: #{CertThumbprint}
```

## Variable naming convention

Octopus project variables named `WeftParam.<ParamName>` get mapped to `WEFT_PARAM_<ParamName>` env vars by the step template, so your `weft.yaml` parameter values can flow from Octopus scopes.
