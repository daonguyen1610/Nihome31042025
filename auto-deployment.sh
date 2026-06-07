# !/usr/bin/env bash

# Step to automate the deployment to hosting
# See https://vietnamconstruction.info:8443/smb/file-manager/list/domainId/184

# Important note:
# Whenever the customer change the images/, this will cause the data mismatch between the publish folder and the deployment-config folder.
# In that case, we need to run this script to update the publish folder in deployment-config.
# So we need to fetch the images/ folder from host to reupdate and then run this script.

die() {
    echo "$1" >&2
    exit 1
}

# Step 1: Build the publish folder from nihomebackend
ROOT_DIR=$(git rev-parse --show-toplevel)
BACKEND_DIR=$ROOT_DIR/nihomebackend
cd $BACKEND_DIR
echo "Cleaning up the previous publish folder..."
rm -rf $BACKEND_DIR/publish
echo "Building the publish folder from nihomebackend..."
dotnet publish -c Release -o publish \
    || die "Failed to build the publish folder."
echo "Publish folder built successfully."

# Step 2: Copy the publish folder to deployment-config
echo "Copying publish folder to deployment-config..."
DEPLOYMENT_CONFIG_DIR=$ROOT_DIR/deployment-config
PUBLISH_RELEASE_DIR=$DEPLOYMENT_CONFIG_DIR/publish-release
cp -r $BACKEND_DIR/publish $PUBLISH_RELEASE_DIR \
    || die "Failed to copy the $PUBLISH_RELEASE_DIR folder."
echo "Publish folder copied to deployment-config."

# Step 3: Copy the deployment-config/images, appsettings.json, and web.config to deployment publish-extract
echo "Copying images, appsettings.json, and web.config to $PUBLISH_RELEASE_DIR..."
cp -rf $DEPLOYMENT_CONFIG_DIR/images $PUBLISH_RELEASE_DIR/wwwroot/images \
    || die "Failed to copy images to $PUBLISH_RELEASE_DIR."
cp -f $DEPLOYMENT_CONFIG_DIR/appsettings.json $PUBLISH_RELEASE_DIR/appsettings.json \
    || die "Failed to copy appsettings.json to $PUBLISH_RELEASE_DIR."
cp -f $DEPLOYMENT_CONFIG_DIR/web.config $PUBLISH_RELEASE_DIR/web.config \
    || die "Failed to copy web.config to $PUBLISH_RELEASE_DIR."
echo "Images, appsettings.json, and web.config copied successfully."

# Step 4: Zip the publish-release folder
echo "Zipping the $PUBLISH_RELEASE_DIR folder..."
zip -r $PUBLISH_RELEASE_DIR $DEPLOYMENT_CONFIG_DIR > /dev/null \
    || die "Failed to zip the $PUBLISH_RELEASE_DIR folder."
echo "Publish folder zipped successfully."