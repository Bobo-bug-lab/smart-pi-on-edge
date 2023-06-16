#include <ESP8266WiFi.h>
#include <PubSubClient.h>
#include "Light_sensor.h"

// Define pins for sensors
#define PIN_PIR D2
#define PIN_LIGHT_SENSOR A0

static Light_sensor* light_sensor;
static int light_value=0,detect=0;

// WiFi参数
const char* ssid = "Vodafone-63Hao";
const char* password = "liushisan.4";

// MQTT参数
const char* mqtt_server = "192.168.1.17";
const int mqtt_port = 1888;
const char* mqtt_topic = "esp";

WiFiClient espClient;
PubSubClient client(espClient);

void setup() {
  Serial.begin(115200);
  
  // 连接WiFi
  WiFi.begin(ssid, password);
  while (WiFi.status() != WL_CONNECTED) {
    delay(500);
    Serial.print(".");
  }
  Serial.println("");
  Serial.print("WiFi connected, IP address: ");
  Serial.println(WiFi.localIP());

  light_sensor = new Light_sensor(PIN_LIGHT_SENSOR);
  pinMode(PIN_PIR, INPUT_PULLUP);

  // 设置MQTT服务器和回调函数
  client.setServer(mqtt_server, mqtt_port);
  client.setCallback(callback);
  
  // 连接MQTT服务器
  reconnect();
}

void loop() {
  // 保持与MQTT服务器的连接
  if (!client.connected()) {
    reconnect();
  }
  
  // 处理MQTT消息
  client.loop();

  // 发送传感器数据
   light_value = light_sensor->getLightLevel();
   detect = digitalRead(PIN_PIR);
   Serial.println("detect_status: ");
   Serial.println(detect);
   Serial.println("light_value: ");
   Serial.println(light_value);


  char payload[20];
  sprintf(payload, "{\"light\": %d, \"detect\": %d}", light_value, detect);
  client.publish(mqtt_topic, payload);  // 发布消息到MQTT主题
  delay(5000);
}

// MQTT回调函数
void callback(char* topic, byte* payload, unsigned int length) {
  Serial.print("Message arrived on topic: ");
  Serial.print(topic);
  Serial.print(". Message: ");
  
  for (int i = 0; i < length; i++) {
    Serial.print((char)payload[i]);
  }
  Serial.println();
}

// 重新连接MQTT服务器
void reconnect() {
  // 尝试连接直到成功
  while (!client.connected()) {
    Serial.print("Attempting MQTT connection...");
    
    // 生成客户端ID（根据需要修改）
    String clientId = "ESP8266Client";
    clientId += String(random(0xffff), HEX);

    // 尝试连接
    if (client.connect(clientId.c_str())) {
      Serial.println("connected");
      client.subscribe(mqtt_topic);  // 订阅MQTT主题
    } else {
      Serial.print("failed, rc=");
      Serial.print(client.state());
      Serial.println(" try again in 5 seconds");
      delay(5000);
    }
  }
}
