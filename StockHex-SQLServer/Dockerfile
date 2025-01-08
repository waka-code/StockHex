# Usar la imagen oficial de SQL Server
FROM mcr.microsoft.com/mssql/server:2022-latest

# Establecer variables de entorno necesarias
ENV ACCEPT_EULA=Y
ENV SA_PASSWORD=SA_PASSWORD

# Configurar el puerto que expone SQL Server
EXPOSE 1433

# Comando de entrada para iniciar SQL Server
CMD ["/opt/mssql/bin/sqlservr"]
