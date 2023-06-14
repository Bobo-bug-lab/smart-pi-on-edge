#include <ESP8266WiFi.h>
#include <PubSubClient.h>

// WiFi参数
const char* ssid = "YOUR_WIFI_SSID";
const char* password = "YOUR_WIFI_PASSWORD";

// MQTT参数
const char* mqtt_server = "YOUR_MQTT_BROKER_IP";
const int mqtt_port = 1883;
const char* mqtt_topic = "YOUR_MQTT_TOPIC";

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
  float sensorValue = analogRead(A0) * 0.0032258;  // 读取传感器数据
  char payload[10];
  dtostrf(sensorValue, 4, 2, payload);  // 转换为字符串
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
    String clientId = "ESP8266Client-";
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
