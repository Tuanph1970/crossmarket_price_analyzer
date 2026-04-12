#!/bin/bash
# Health check for .NET API services
curl -f http://localhost:8080/health || exit 1
