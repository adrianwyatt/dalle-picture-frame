# DALL-E Picture Frame
A DALL-E 2 powered voice activated picture frame.

This was originally written to run on a Raspberry PI with a USB speakerphone attached. I used a Raspberry PI 4 running Raspbian and the [INNOTRIK Bluetooth Conference Speaker with Microphone](https://www.amazon.com/INNOTRIK-Conference-Microphone-Omnidirectional-Speakerphone/dp/B098DKS637) attached. It's the same hardware used in my [Conversation Speaker](https://github.com/microsoft/conversational-speaker) project.

## Setup
### 1. Azure Cognitive Services
You will need to deploy an instance of Azure Cognitive Services for the Speech-To-Text (https://azure.microsoft.com/en-us/products/cognitive-services/). You get 5 hours of audio processing for free and it's relatively inexpensive after that.

### 2. OpenAI's DALL-E 2
You will need to sign up for OpenAI's DALL-E 2 (https://openai.com/dall-e-2/). You get 50 free credits when signing up and 15 free credits every month after that. For more, you can spend $15 to get enough credits for 460 image requests. Note that this application uses *three* image requests per rendering to attain 1080p.

### 3. 'feh' image viewer
The slideshow uses `feh` to run. Simply run `sudo apt install feh` from your linux shell (e.g. bash).
### 3. dotnet 6.x SDK
You will need to install the dotnet 6.x *SDK*: https://dotnet.microsoft.com/en-us/download/dotnet/6.0

### 3. Set Configuration
Look at `./configuration.json` and run each of the `dotnet user-secrets set` commands before building. This adds the keys and endpoints for Azure Cognitive Services and OpenAI to the application.

### 4. Build and run
From the `/src/dalleframecon` directory, run `dotnet run` and that should start the show.

## How it works
The application...
1. Starts and starts a separate process showing a slideshow of previous images.
2. Starts listening to you for the wake phrase, "Hey, DALL-E" (pronounced 'Hey Dolly').
3. Then sounds a notification 'bing!'.
4. Listens to you for what to render (e.g. "A 3D render of a white furry monster in a purple room.").
5. Responds with "Sure thing, one moment."
6. Then updates with "Just adding a few more details."
7. On completion says "Here you go!".
8. Then restarts the randomized slideshow of previous images, starting with the image just requested.
9. And finally starts listening for the wake phrase again.

## Notes
- The application makes *three* calls to DALL-E in order to render a 1080p image. 
  - DALL-E itself can only render a 1024x1024 image at the largest. The application resends the first image 
    back to DALL-E and requests a render the remaining left and right images, then the application stiches it all together for proper aspect ratio and upscales it to 1080p.
- Images are stored in the application's directory under a sub-directory "images".
- The application may not be able to start the slideshow if there are no images in the "images" directory.
- The application does not handle error codes from OpenAI or Cognitive Services. If some returns a failure, such as for content violations, the application will stop listening but continue continue the slideshow.
