import asyncio
import sys
import signal
import threading
import json
import cv2
import numpy as np
from azure.iot.device.aio import IoTHubModuleClient
from azure.iot.device import Message

import os

# global counters
PIC_DIFF_THRESHOLD = 0.3
TWIN_CALLBACKS = 0
# Event indicating client stop
stop_event = threading.Event()


def create_client():
    client = IoTHubModuleClient.create_from_edge_environment()
    # twin_patch_listener is invoked when the module twin's desired properties are updated.
    async def receive_twin_patch_handler(twin_patch):
        global PIC_DIFF_THRESHOLD
        global TWIN_CALLBACKS
        print("Twin Patch received")
        print("     {}".format(twin_patch))
        if "PIC_DIFF_THRESHOLD" in twin_patch:
            PIC_DIFF_THRESHOLD = twin_patch["PIC_DIFF_THRESHOLD"]
        TWIN_CALLBACKS += 1
        print("Total calls confirmed: {}".format(TWIN_CALLBACKS))

    try:
        # Set handler on the client
        client.on_twin_desired_properties_patch_received = receive_twin_patch_handler
    except:
        # Cleanup if failure occurs
        client.shutdown()
        raise    

    return client

def mse(img1, img2):
   h, w = img1.shape
   diff = cv2.subtract(img1, img2)
   err = np.sum(diff**2)
   mse = err / (float(h * w))
   return mse, diff

async def run_sample(client):
    # Customize this coroutine to do whatever tasks the module initiates
    # e.g. sending messages
    while True:
        await asyncio.sleep(5)

        base_image = cv2.imread('img0.jpg')
        base_image = cv2.cvtColor(base_image, cv2.COLOR_BGR2GRAY)

        # Loop through the images
        for i in range(5):
            img_path = f"img{i}.jpg"

            if os.path.exists(img_path):
                # Load the image
                img = cv2.imread(img_path)

                # Convert the image to grayscale
                gray_img = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)

                # Calculate the image features (e.g. size, channels, etc.)
                height, width, channels = img.shape

                print(f"Image: {img_path}")
                print(f"Size: {width}x{height}")
                print(f"Channels: {channels}")
                print(f"Gray image shape: {gray_img.shape}")
                print("")
                error, diff = mse(base_image, gray_img)
                print(f"Image matching Error between img{i}.jpg and img0.jpg: {error}")
                message_body = json.dumps({"pic_diff": error})
                output_message = Message(message_body)
                output_message.content_encoding = "utf-8"
                output_message.content_type = "application/json"
                await client.send_message_to_output(output_message, "output1")
                await client.patch_twin_reported_properties({"pic_diff": error})



                print("Finished processing images")             

def main():
    if not sys.version >= "3.5.3":
        raise Exception("The sample requires python 3.5.3+. Current version of Python: %s" % sys.version)
    print("IoT Hub Client for Python")

    # NOTE: Client is implicitly connected due to the handler being set on it
    client = create_client()

    # Define a handler to cleanup when module is terminated by Edge
    def module_termination_handler(signal, frame):
        print("IoTHubClient sample stopped by Edge")
        stop_event.set()

    # Set the Edge termination handler
    signal.signal(signal.SIGTERM, module_termination_handler)

    # Run the sample
    loop = asyncio.get_event_loop()
    try:
        loop.run_until_complete(run_sample(client))
    except Exception as e:
        print("Unexpected error %s " % e)
        raise
    finally:
        print("Shutting down IoT Hub Client...")
        loop.run_until_complete(client.shutdown())
        loop.close()


if __name__ == "__main__":
    main()
