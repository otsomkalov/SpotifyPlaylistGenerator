# Spotify Playlist Generator

Spotify Playlist Generator is a tool designed to help you create a unique and customized playlist by exporting tracks from various sources. This README provides an informative guide on how the application works and how to run it.

## How it Works

The Spotify Playlist Generator follows a specific workflow to create a new playlist with unique tracks. Here's a step-by-step explanation of its operation:

1. **Load Liked Tracks**: The application starts by loading the list of tracks you have liked on Spotify. These are the songs you've shown an interest in.

2. **Load Listened Tracks from Playlist**: The tool then fetches a list of tracks that you've already listened to from a specified playlist. This helps ensure that the generated playlist doesn't include songs you've already heard.

3. **Load Tracks from Specified Playlists**: The application also loads tracks from other playlists you've specified in the configuration. This allows you to include songs from your favorite curated playlists or collections.

4. **Clean Tracks**: The tracks loaded in the previous steps are cleaned to remove duplicates and any songs that you've liked or already listened to. This step ensures that the playlist remains unique and free from redundant tracks.

5. **Shuffle Tracks**: The application shuffles the cleaned list of tracks to add an element of randomness and surprise to your playlist.

6. **Select Only 100 Tracks**: To keep the playlist manageable and of a reasonable length, the tool selects the top 100 tracks from the shuffled list. You can adjust this number in the configuration if you prefer a longer or shorter playlist.

7. **Save to Target Playlist**: The selected 100 tracks are saved to a target playlist of your choice. This is your newly generated playlist, unique and tailored to your preferences.

8. **Update Playlist with Listened Tracks**: The playlist containing tracks you've already listened to is updated to reflect the addition of new tracks. This ensures that you have an accurate record of your music history.

## How to Run

To run the Spotify Playlist Generator, follow these steps:

1. **Update appsettings.json**: Open the `appsettings.json` file and provide the necessary configurations. You should specify your Spotify API credentials, the target playlist where the generated playlist will be saved, and the playlist from which you want to exclude already listened tracks.

2. **Run the Application**: Open your terminal or command prompt, navigate to the project directory, and execute the following command:


3. The application will start executing the workflow described above, and you will soon have your unique playlist in your Spotify account.

Make sure you have the .NET runtime installed on your system before running the application. The application relies on Spotify's API, so you'll need to set up your Spotify developer credentials and permissions as per their guidelines.

Enjoy your personalized, unique playlist!

