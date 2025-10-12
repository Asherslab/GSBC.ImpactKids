{{- if .Release.IsInstall }}
sqlPass: {{ randAlphaNum 20 | b64enc }}
rabbitmqPass: {{ randAlphaNum 20 | b64enc }}
cachePass: {{ randAlphaNum 20 | b64enc }}
{{ else }}
sqlPass: {{ index (lookup "v1" "Secret" .Release.Namespace "sql-secrets").data "MSSQL_SA_PASSWORD" }}
rabbitmqPass: {{ index (lookup "v1" "Secret" .Release.Namespace "rabbitmq-secrets").data "RABBITMQ_DEFAULT_PASS" }}
cachePass: {{ index (lookup "v1" "Secret" .Release.Namespace "cache-secrets").data "REDIS_PASSWORD" }}
{{ end }}