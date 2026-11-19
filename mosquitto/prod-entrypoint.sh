#!/bin/sh
# Writes the broker's password file on every start, so the Portainer stack needs no file on the
# host and changing MQTT_PASSWORD in the stack takes effect on the next deploy. The password
# goes through a 0600 file and is hashed in place, never via the command line (visible in ps).
set -eu
: "${MQTT_USERNAME:?MQTT_USERNAME must be set}"
: "${MQTT_PASSWORD:?MQTT_PASSWORD must be set}"
# mosquitto_passwd -U splits "user:password" on the colon: a ':' in the password yields a hash
# that never matches, and the broker refuses the backend with "not authorised".
case "$MQTT_PASSWORD" in
  *:*) echo "MQTT_PASSWORD must not contain ':' (use e.g. openssl rand -hex 24)." >&2; exit 1 ;;
esac
umask 077
printf '%s:%s\n' "$MQTT_USERNAME" "$MQTT_PASSWORD" > /mosquitto/config/passwd
unset MQTT_PASSWORD
mosquitto_passwd -U /mosquitto/config/passwd
chown 1883:1883 /mosquitto/config/passwd
exec /docker-entrypoint.sh "$@"
