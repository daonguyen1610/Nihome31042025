# !/usr/bin/env bash

# Step to automate the deployment to hosting
# See https://vietnamconstruction.info:8443/smb/file-manager/list/domainId/184

# Important note:
# Whenever the customer change the images/, this will cause the data mismatch between the publish folder and the deployment-config folder.
# In that case, we need to run this script to update the publish folder in deployment-config.
# So we need to fetch the images/ folder from host to reupdate and then run this script.

ROOT_DIR=$(git rev-parse --show-toplevel)
BACKEND_DIR=$ROOT_DIR/nihomebackend
DEPLOYMENT_CONFIG_DIR=$ROOT_DIR/deployment-config
OUTPUT_DIR=$DEPLOYMENT_CONFIG_DIR/output
PUBLISH_RELEASE_DIR=$OUTPUT_DIR/publish-release

die() {
    echo "$1" >&2
    exit 1
}

cleanup() {
    echo "Cleaning up the publish folder in deployment-config..."
    echo "Cleaning up the previous publish folder..."
    rm -rf $BACKEND_DIR/publish \
        || die "Failed to clean up the publish folder in nihomebackend."
    rm -rf $OUTPUT_DIR \
        || die "Failed to clean up the output directory."
    echo "Cleanup completed."
}

init_output() {
    echo "Initializing the output directory..."
    cleanup
    mkdir -p $OUTPUT_DIR \
        || die "Failed to create the output directory."
    echo "Output directory initialized."
}

init_output

# Step 1: Build the publish folder from nihomebackend
cd $BACKEND_DIR
echo "Building the publish folder from nihomebackend..."
dotnet publish -c Release -o publish \
    || die "Failed to build the publish folder."
echo "Publish folder built successfully."

# Step 2: Copy the publish folder to deployment-config
echo "Copying publish folder to deployment-config..."
cp -r $BACKEND_DIR/publish $PUBLISH_RELEASE_DIR \
    || die "Failed to copy the $PUBLISH_RELEASE_DIR folder."
echo "Publish folder copied to deployment-config."

# Step 3: Copy the deployment-config/images, appsettings.json, and web.config to deployment publish-extract
echo "Copying images, process-assets, processes, appsettings.json, and web.config to $PUBLISH_RELEASE_DIR..."
# Copy images (data). Use src/. + mkdir -p so re-runs don't nest dirs
# (cp -r src dest copies INTO dest when dest already exists on BSD/macOS).
mkdir -p $PUBLISH_RELEASE_DIR/wwwroot/images $PUBLISH_RELEASE_DIR/wwwroot/process-assets $PUBLISH_RELEASE_DIR/wwwroot/processes \
    || die "Failed to create wwwroot data directories."
cp -rf $DEPLOYMENT_CONFIG_DIR/images/. $PUBLISH_RELEASE_DIR/wwwroot/images/ \
    || die "Failed to copy images to $PUBLISH_RELEASE_DIR."
# Copy process-assets, processes (data)
cp -rf $DEPLOYMENT_CONFIG_DIR/process-assets/. $PUBLISH_RELEASE_DIR/wwwroot/process-assets/ \
    || die "Failed to copy process-assets to $PUBLISH_RELEASE_DIR."
cp -rf $DEPLOYMENT_CONFIG_DIR/processes/. $PUBLISH_RELEASE_DIR/wwwroot/processes/ \
    || die "Failed to copy processes to $PUBLISH_RELEASE_DIR."

# Copy appsettings.json and web.config (config files)
cp -f $DEPLOYMENT_CONFIG_DIR/appsettings.json $PUBLISH_RELEASE_DIR/appsettings.json \
    || die "Failed to copy appsettings.json to $PUBLISH_RELEASE_DIR."
cp -f $DEPLOYMENT_CONFIG_DIR/web.config $PUBLISH_RELEASE_DIR/web.config \
    || die "Failed to copy web.config to $PUBLISH_RELEASE_DIR."
echo "Images, process-assets, processes, appsettings.json, and web.config copied successfully."

# Step 4: Zip the publish-release folder
echo "Zipping the $PUBLISH_RELEASE_DIR folder..."
zip -r $PUBLISH_RELEASE_DIR $DEPLOYMENT_CONFIG_DIR > /dev/null \
    || die "Failed to zip the $PUBLISH_RELEASE_DIR folder."
echo "Publish folder zipped successfully."