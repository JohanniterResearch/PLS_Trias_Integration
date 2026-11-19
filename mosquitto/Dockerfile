# Broker image for docker-compose.prod.yml (Portainer Git stack). The config and ACL are baked
# in because a Git stack has no checkout on the host to bind-mount them from; the password file
# is written at start from MQTT_USERNAME/MQTT_PASSWORD (see prod-entrypoint.sh).
FROM eclipse-mosquitto:2
COPY mosquitto-prod.conf /mosquitto/config/mosquitto.conf
COPY acl /mosquitto/config/acl
COPY prod-entrypoint.sh /usr/local/bin/prod-entrypoint.sh
# The broker refuses or warns about auth files other users can read.
RUN chown 1883:1883 /mosquitto/config/acl && chmod 600 /mosquitto/config/acl
ENTRYPOINT ["/bin/sh", "/usr/local/bin/prod-entrypoint.sh"]
# Setting ENTRYPOINT drops the base image's CMD; restate it.
CMD ["/usr/sbin/mosquitto", "-c", "/mosquitto/config/mosquitto.conf"]
