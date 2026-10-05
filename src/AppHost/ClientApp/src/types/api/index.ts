import { Authentication } from '@api/generated/Authentication';
import { BackgroundJobs } from '@api/generated/BackgroundJobs';
import { Download } from '@api/generated/Download';
import { FolderPath } from '@api/generated/FolderPath';
import { Notification } from '@api/generated/Notification';
import { PlexAccount } from '@api/generated/PlexAccount';
import { PlexLibrary } from '@api/generated/PlexLibrary';
import { PlexMedia } from '@api/generated/PlexMedia';
import { PlexServer } from '@api/generated/PlexServer';
import { PlexServerConnection } from '@api/generated/PlexServerConnection';
import { Settings } from '@api/generated/Settings';
import { Integration } from '@api/generated/Integration';
import { Update } from '@api/generated/Update';
import { Debug } from '@api/generated/Debug';

export * from './api-paths';
export * from './custom';

const authenticationApi = new Authentication();
const backgroundJobsApi = new BackgroundJobs();
const downloadApi = new Download();
const debugApi = new Debug();
const folderPathApi = new FolderPath();
const notificationApi = new Notification();
const plexAccountApi = new PlexAccount();
const plexLibraryApi = new PlexLibrary();
const plexMediaApi = new PlexMedia();
const plexServerApi = new PlexServer();
const plexServerConnectionApi = new PlexServerConnection();
const integrationApi = new Integration();
const settingsApi = new Settings();
const updateApi = new Update();

export {
	authenticationApi,
	backgroundJobsApi,
	downloadApi,
	debugApi,
	folderPathApi,
	notificationApi,
	plexAccountApi,
	plexLibraryApi,
	plexMediaApi,
	plexServerApi,
	plexServerConnectionApi,
	settingsApi,
	integrationApi,
	updateApi,
};
