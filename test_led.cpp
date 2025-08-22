#include <WiFi.h>
#include <WiFiUdp.h>
#include <Adafruit_NeoPixel.h>
// --------------------------------------------------
// LED Strip Settings
// --------------------------------------------------
#define LED_PIN    23
#define NUM_LEDS   114 // Set this to match your total number of LEDs
Adafruit_NeoPixel strip(NUM_LEDS, LED_PIN, NEO_GRB + NEO_KHZ800);
uint8_t ledColors[NUM_LEDS][3]; // Store previous colors

// --------------------------------------------------
// Wi-Fi Credentials
// --------------------------------------------------
const char* ssid     = "IOT_NETWORK";
const char* password = "@Passw0rd#";
const int udpPort = 7777; // Match this with your backend

// --------------------------------------------------
// Persistent preferences and web server
// --------------------------------------------------
WiFiUDP udp;

void setup() {
  WiFi.setSleep(false); // keep WiFi responsive
  strip.begin();
  strip.show(); // Initialize all pixels to 'off'
  Serial.begin(115200);
  WiFi.begin(ssid, password);
  Serial.print("Connecting to WiFi");
  while (WiFi.status() != WL_CONNECTED) { delay(500); Serial.print("."); }
  Serial.println("\nWiFi connected!");
  Serial.print("IP address: "); Serial.println(WiFi.localIP());
  udp.begin(udpPort);
  Serial.print("Listening on UDP port "); Serial.println(udpPort);
}

void loop() {
  int packetSize = udp.parsePacket();
  if (packetSize > 0) {
    static char buf[4096];
    int toRead = packetSize;
    if (toRead >= (int)sizeof(buf)) toRead = sizeof(buf) - 1;
    int len = udp.read(buf, toRead);
    if (len <= 0) { udp.flush(); delay(0); return; }
    buf[len] = 0; // NUL-terminate

    int ledIndex = 0;
    int comp = 0; // 0=R, 1=G, 2=B
    int val = 0;
    bool inNum = false;
    uint8_t rgb[3] = {0,0,0};
    bool anyChange = false;

    for (int i = 0; i < len && ledIndex < NUM_LEDS; ++i) {
      char c = buf[i];
      if (c >= '0' && c <= '9') { inNum = true; val = val * 10 + (c - '0'); if (val > 255) val = 255; }
      if (c == ',' || c == '\n' || c == '\r' || i == len - 1) {
        if (inNum || i == len - 1) {
          rgb[comp++] = (uint8_t)val;
          if (comp == 3) {
            if (ledColors[ledIndex][0] != rgb[0] || ledColors[ledIndex][1] != rgb[1] || ledColors[ledIndex][2] != rgb[2]) {
              ledColors[ledIndex][0] = rgb[0];
              ledColors[ledIndex][1] = rgb[1];
              ledColors[ledIndex][2] = rgb[2];
              strip.setPixelColor(ledIndex, strip.Color(rgb[0], rgb[1], rgb[2]));
              anyChange = true;
            }
            comp = 0; ledIndex++;
          }
          val = 0; inNum = false;
        }
      }
    }

    if (anyChange) strip.show();
    udp.flush();
  }
  delay(0); // yield to WiFi
}
 