#!/bin/bash
# Health check for .NET API services — zero-dependency (uses bash built-in /dev/tcp)
exec 3<>/dev/tcp/127.0.0.1/8080
printf 'GET /health HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n' >&3
timeout 3 grep -q '200' <&3
