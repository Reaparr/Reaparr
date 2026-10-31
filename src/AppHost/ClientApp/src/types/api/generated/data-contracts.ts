/* eslint-disable */
/* tslint:disable */
// @ts-nocheck
/*
 * ---------------------------------------------------------------
 * ## THIS FILE WAS GENERATED VIA SWAGGER-TYPESCRIPT-API        ##
 * ##                                                           ##
 * ## AUTHOR: acacode                                           ##
 * ## SOURCE: https://github.com/acacode/swagger-typescript-api ##
 * ---------------------------------------------------------------
 */

export interface AppCredentialsDTO {
  isDefaultCredentials: boolean;
  password: string;
  userName: string;
}

export interface AppUpdateCheckDTO {
  currentVersion: string;
  isUpdateAvailable: boolean;
  newestVersion: string;
  releaseNotes: IReadOnlyListOfReleaseNoteDTO;
}

export interface AppUpdateDownloadProgressDTO {
  isComplete: boolean;
  /** @format int32 */
  percentage: number;
}

/** @example {"username":"ReaparrRocks","password":"R€Aℙℙ@rr69","rememberMe":false} */
export interface AppUserLoginEndpointRequest {
  /**
   * @minLength 1
   * @default "R€Aℙℙ@rr69"
   * @example "R€Aℙℙ@rr69"
   */
  password: string;
  /**
   * @default false
   * @example false
   */
  rememberMe: boolean;
  /**
   * @minLength 1
   * @default "ReaparrRocks"
   * @example "ReaparrRocks"
   */
  username: string;
}

export enum AudioQuality {
  Unknown = "Unknown",
  MP3Below96Kbps = "MP3_Below96Kbps",
  MP396Kbps = "MP3_96Kbps",
  MP3128Kbps = "MP3_128Kbps",
  MP3160Kbps = "MP3_160Kbps",
  MP3192Kbps = "MP3_192Kbps",
  MP3256Kbps = "MP3_256Kbps",
  MP3320KbpsPlus = "MP3_320KbpsPlus",
  AACBelow96Kbps = "AAC_Below96Kbps",
  AAC96Kbps = "AAC_96Kbps",
  AAC128Kbps = "AAC_128Kbps",
  AAC160Kbps = "AAC_160Kbps",
  AAC192Kbps = "AAC_192Kbps",
  AAC256Kbps = "AAC_256Kbps",
  AAC320KbpsPlus = "AAC_320KbpsPlus",
  VorbisBelow96Kbps = "Vorbis_Below96Kbps",
  Vorbis96Kbps = "Vorbis_96Kbps",
  Vorbis128Kbps = "Vorbis_128Kbps",
  Vorbis160Kbps = "Vorbis_160Kbps",
  Vorbis192Kbps = "Vorbis_192Kbps",
  Vorbis256Kbps = "Vorbis_256Kbps",
  Vorbis320KbpsPlus = "Vorbis_320KbpsPlus",
  OpusBelow96Kbps = "Opus_Below96Kbps",
  Opus96Kbps = "Opus_96Kbps",
  Opus128Kbps = "Opus_128Kbps",
  Opus160Kbps = "Opus_160Kbps",
  Opus192Kbps = "Opus_192Kbps",
  Opus256Kbps = "Opus_256Kbps",
  Opus320KbpsPlus = "Opus_320KbpsPlus",
  FLAC16Bit441KHz = "FLAC_16Bit_44_1kHz",
  FLAC16Bit48KHz = "FLAC_16Bit_48kHz",
  FLAC16Bit882KHz = "FLAC_16Bit_88_2kHz",
  FLAC16Bit96KHz = "FLAC_16Bit_96kHz",
  FLAC16Bit1764KHz = "FLAC_16Bit_176_4kHz",
  FLAC16Bit192KHz = "FLAC_16Bit_192kHz",
  FLAC24Bit441KHz = "FLAC_24Bit_44_1kHz",
  FLAC24Bit48KHz = "FLAC_24Bit_48kHz",
  FLAC24Bit882KHz = "FLAC_24Bit_88_2kHz",
  FLAC24Bit96KHz = "FLAC_24Bit_96kHz",
  FLAC24Bit1764KHz = "FLAC_24Bit_176_4kHz",
  FLAC24Bit192KHz = "FLAC_24Bit_192kHz",
  ALAC16Bit441KHz = "ALAC_16Bit_44_1kHz",
  ALAC16Bit48KHz = "ALAC_16Bit_48kHz",
  ALAC16Bit882KHz = "ALAC_16Bit_88_2kHz",
  ALAC16Bit96KHz = "ALAC_16Bit_96kHz",
  ALAC16Bit1764KHz = "ALAC_16Bit_176_4kHz",
  ALAC16Bit192KHz = "ALAC_16Bit_192kHz",
  ALAC24Bit441KHz = "ALAC_24Bit_44_1kHz",
  ALAC24Bit48KHz = "ALAC_24Bit_48kHz",
  ALAC24Bit882KHz = "ALAC_24Bit_88_2kHz",
  ALAC24Bit96KHz = "ALAC_24Bit_96kHz",
  ALAC24Bit1764KHz = "ALAC_24Bit_176_4kHz",
  ALAC24Bit192KHz = "ALAC_24Bit_192kHz",
  PCM16Bit441KHz = "PCM_16Bit_44_1kHz",
  PCM16Bit48KHz = "PCM_16Bit_48kHz",
  PCM16Bit882KHz = "PCM_16Bit_88_2kHz",
  PCM16Bit96KHz = "PCM_16Bit_96kHz",
  PCM16Bit1764KHz = "PCM_16Bit_176_4kHz",
  PCM16Bit192KHz = "PCM_16Bit_192kHz",
  PCM24Bit441KHz = "PCM_24Bit_44_1kHz",
  PCM24Bit48KHz = "PCM_24Bit_48kHz",
  PCM24Bit882KHz = "PCM_24Bit_88_2kHz",
  PCM24Bit96KHz = "PCM_24Bit_96kHz",
  PCM24Bit1764KHz = "PCM_24Bit_176_4kHz",
  PCM24Bit192KHz = "PCM_24Bit_192kHz",
  None = "None",
}

export interface BaseResultDTO {
  errors: IReadOnlyListOfErrorDTO;
  isSuccess: boolean;
  /** @format int32 */
  statusCode: number;
  successes: IReadOnlyListOfSuccessDTO;
}

export interface CheckAllConnectionStatusUpdateDTO {
  plexServersWithConnectionIds: DictionaryOfInt32AndListOfInt32;
}

export interface ConfirmationSettingsDTO {
  askDownloadEpisodeConfirmation: boolean;
  askDownloadMovieConfirmation: boolean;
  askDownloadMusicAlbumConfirmation: boolean;
  askDownloadMusicArtistConfirmation: boolean;
  askDownloadMusicTrackConfirmation: boolean;
  askDownloadOtherVideosConfirmation: boolean;
  askDownloadPhotoAlbumConfirmation: boolean;
  askDownloadPhotoImageConfirmation: boolean;
  askDownloadSeasonConfirmation: boolean;
  askDownloadTvShowConfirmation: boolean;
}

export interface CountResponseDTO {
  /** @format int32 */
  count: number;
}

export interface CreateDownloadTasksRequest {
  /** @default "" */
  customDestinationFolderPath: string;
  /** @format int32 */
  destinationFolderPathId?: number | null;
  downloadMedias: ListOfDownloadMediaDTO;
  integration?: IntegrationIdentity | null;
}

export interface CreatePlexAccountEndpointRequest {
  is2Fa: boolean;
  /** @minLength 5 */
  authenticationToken: string;
  /** @minLength 1 */
  clientId: string;
  customAuthenticationToken: string;
  /** @minLength 1 */
  displayName: string;
  email: string;
  isEnabled: boolean;
  isMain: boolean;
  isValidated: boolean;
  /** @minLength 5 */
  password: string;
  /** @format int64 */
  plexId: number;
  title: string;
  /** @minLength 5 */
  username: string;
  /** @minLength 1 */
  uuid: string;
  /** @format date-time */
  validatedAt: string;
}

export interface CreatePlexServerConnectionEndpointRequest {
  /** @minLength 1 */
  address: string;
  /**
   * @format int32
   * @min 0
   * @exclusiveMin true
   */
  plexServerId: number;
  /**
   * @format int32
   * @min 0
   * @exclusiveMin true
   */
  port: number;
  /** @minLength 1 */
  protocol: string;
  /** @minLength 1 */
  url: string;
}

export interface CreateRadarrIntegrationRequest {
  /** @minLength 1 */
  apiKey: string;
  /** @minLength 1 */
  category: string;
  /**
   * @format int32
   * @min 0
   * @exclusiveMin true
   */
  downloadFolderId: number;
  /** @minLength 1 */
  name: string;
  /** @minLength 1 */
  url: string;
}

export interface CreateSonarrIntegrationRequest {
  /** @minLength 1 */
  apiKey: string;
  /** @minLength 1 */
  category: string;
  /**
   * @format int32
   * @min 0
   * @exclusiveMin true
   */
  downloadFolderId: number;
  /** @minLength 1 */
  name: string;
  /** @minLength 1 */
  url: string;
}

export interface DateTimeSettingsDTO {
  longDateFormat: string;
  shortDateFormat: string;
  showRelativeDates: boolean;
  timeFormat: string;
  timeZone: string;
}

export interface DebugSettingsDTO {
  debugModeEnabled: boolean;
  maskAccountNames: boolean;
  maskLibraryNames: boolean;
  maskServerNames: boolean;
}

/** Message sent from the front-end to the back-end in Desktop mode. This is for opening external links etc */
export interface DesktopMessageDTO {
  /** The type of the DesktopMessageDTO */
  type: DesktopMessageType;
  /** The value of the DesktopMessageDTO */
  value: string;
}

/** The type of the DesktopMessageDTO */
export enum DesktopMessageType {
  None = "None",
  ExternalLink = "ExternalLink",
  DesktopReady = "DesktopReady",
}

export type DictionaryOfInt32AndListOfInt32 = Record<string, ListOfInt32>;

export type DictionaryOfStringAndBoolean = Record<string, boolean>;

export type DictionaryOfStringAndDictionaryOfStringAndNullableOfInt32 = Record<
  string,
  DictionaryOfStringAndNullableOfInt32
>;

export type DictionaryOfStringAndNullableOfInt32 = Record<
  string,
  number | null
>;

export type DictionaryOfStringAndObject = object;

export interface DisplaySettingsDTO {
  allOverviewViewMode: PlexMediaType;
  movieViewMode: ViewMode;
  musicArtistViewMode: ViewMode;
  otherVideosViewMode: ViewMode;
  photoAlbumViewMode: ViewMode;
  tvShowViewMode: ViewMode;
}

export enum DownloadActions {
  Details = "Details",
  Delete = "Delete",
  Start = "Start",
  Pause = "Pause",
  Stop = "Stop",
  Clear = "Clear",
  Restart = "Restart",
}

export interface DownloadJobUpdateDTO {
  id: DownloadTaskKey;
}

export interface DownloadManagerSettingsDTO {
  downloadSchedule: DownloadScheduleDTO;
  /** @format int32 */
  downloadSegments: number;
  keepCompletedInDownloadFolder: boolean;
}

export interface DownloadMediaDTO {
  keepCompletedInDownloadFolder: boolean;
  mediaIds: ListOfInt32;
  /** @format int32 */
  plexLibraryId: number;
  /** @format int32 */
  plexServerId: number;
  qualities: ListOfPlexMediaQualityDTO;
  selectionKind?: PhotoItemKind | null;
  type: PlexMediaType;
}

export interface DownloadPatchDTO {
  /** @format int64 */
  dataReceived: number;
  /** @format int64 */
  dataTotal: number;
  /** @format int64 */
  downloadSpeed: number;
  /** @format uuid */
  id: string;
  /** @format uuid */
  parentId: string;
  /** @format double */
  percentage: number;
  status: DownloadStatus;
  /** @format int32 */
  timeRemaining: number;
}

export interface DownloadPatchEntryMessagePackDTO {
  /** @format int64 */
  dataReceived: number;
  /** @format int64 */
  dataTotal: number;
  /** @format int64 */
  downloadSpeed: number;
  /** @format uuid */
  id: string;
  /** @format uuid */
  parentId: string;
  /** @format double */
  percentage: number;
  status: DownloadStatus;
  /** @format int32 */
  timeRemaining: number;
}

export interface DownloadPatchMessagePackDTO {
  deletedIds: ListOfGuid;
  /** @format int64 */
  sequence: number;
  /** @format int32 */
  serverId: number;
  upserts: ListOfDownloadPatchEntryMessagePackDTO;
}

export interface DownloadPreviewContainerDTO {
  expanded: DictionaryOfStringAndBoolean;
  previews: ListOfDownloadPreviewDTO;
  /** @format int64 */
  totalSize: number;
}

export interface DownloadPreviewDTO {
  children: ListOfDownloadPreviewDTO;
  key: string;
  qualities: ListOfPlexMediaQualityDTO;
  /** @format int64 */
  size: number;
  title: string;
  type: PlexMediaType;
}

export interface DownloadPreviewRequest {
  customDestinationFolderPath: string;
  /** @format int32 */
  destinationFolderPathId?: number | null;
  downloadMedias: ListOfDownloadMediaDTOOpDF26594E59F4;
}

export interface DownloadProgressDTO {
  children: ListOfDownloadProgressDTO;
  /** @format int64 */
  dataReceived: number;
  /** @format int64 */
  dataTotal: number;
  /** @format int64 */
  downloadSpeed: number;
  /** @format uuid */
  id: string;
  mediaType: PlexMediaType;
  /** @format double */
  percentage: number;
  status: DownloadStatus;
  /** @format int32 */
  timeRemaining: number;
  title: string;
}

export interface DownloadProgressMessagePackDTO {
  children: ListOfDownloadProgressMessagePackDTO;
  /** @format int64 */
  dataReceived: number;
  /** @format int64 */
  dataTotal: number;
  /** @format int64 */
  downloadSpeed: number;
  /** @format uuid */
  id: string;
  mediaType: PlexMediaType;
  /** @format double */
  percentage: number;
  status: DownloadStatus;
  /** @format int32 */
  timeRemaining: number;
  title: string;
}

export interface DownloadScheduleDTO {
  days: DictionaryOfStringAndDictionaryOfStringAndNullableOfInt32;
  enabled: boolean;
}

export enum DownloadStatus {
  Unknown = "Unknown",
  Error = "Error",
  Queued = "Queued",
  Downloading = "Downloading",
  DownloadFinished = "DownloadFinished",
  Moving = "Moving",
  MovePaused = "MovePaused",
  AutoMovePaused = "AutoMovePaused",
  MoveFinished = "MoveFinished",
  Completed = "Completed",
  Paused = "Paused",
  AutoPaused = "AutoPaused",
  Stopped = "Stopped",
  Deleted = "Deleted",
  ServerUnreachable = "ServerUnreachable",
  AuthError = "AuthError",
  StorageError = "StorageError",
  SourceUnavailable = "SourceUnavailable",
  DownloadClientError = "DownloadClientError",
  IntegrityError = "IntegrityError",
  MoveError = "MoveError",
  Restarting = "Restarting",
}

export interface DownloadTaskCreationReportDTO {
  /** @format int32 */
  episodes: number;
  /** @format int32 */
  movies: number;
  /** @format int32 */
  musicAlbums: number;
  /** @format int32 */
  musicArtists: number;
  /** @format int32 */
  musicTracks: number;
  /** @format int32 */
  otherVideos: number;
  /** @format int32 */
  photoFolders: number;
  /** @format int32 */
  photoImages: number;
  /** @format int32 */
  seasons: number;
  /** @format int32 */
  total: number;
  /** @format int32 */
  tvShows: number;
}

export interface DownloadTaskDTO {
  audioQuality: AudioQuality;
  children: ListOfDownloadTaskDTO;
  /** @format date-time */
  createdAt: string;
  /** @format int64 */
  dataReceived: number;
  /** @format int64 */
  dataTotal: number;
  destinationDirectory: string;
  downloadDirectory: string;
  /** @format int64 */
  downloadSpeed: number;
  downloadTaskType: DownloadTaskType;
  downloadUrl: string;
  fileLocationUrl: string;
  fileName: string;
  /** @format int64 */
  fileTransferSpeed: number;
  fullTitle: string;
  /** @format uuid */
  id: string;
  mediaType: PlexMediaType;
  /** @format uuid */
  parentId: string;
  /** @format double */
  percentage: number;
  /** @format int32 */
  plexLibraryId: number;
  /** @format int32 */
  plexServerId: number;
  /** @format int64 */
  ratingKey: number;
  status: DownloadStatus;
  /** @format int32 */
  timeRemaining: number;
  title: string;
}

export interface DownloadTaskKey {
  /** @format uuid */
  id: string;
  isValid: boolean;
  /** @format int32 */
  plexLibraryId: number;
  /** @format int32 */
  plexServerId: number;
  type: DownloadTaskType;
}

export interface DownloadTaskLogDTO {
  /** @format date-time */
  createdAt: string;
  /** @format int32 */
  id: number;
  logLevel: NotificationLevel;
  message: string;
  status: DownloadStatus;
}

export enum DownloadTaskType {
  None = "None",
  Movie = "Movie",
  MovieData = "MovieData",
  TvShow = "TvShow",
  Season = "Season",
  Episode = "Episode",
  EpisodeData = "EpisodeData",
  MusicArtist = "MusicArtist",
  MusicAlbum = "MusicAlbum",
  MusicTrack = "MusicTrack",
  MusicTrackData = "MusicTrackData",
  PhotoFolder = "PhotoFolder",
  PhotoImage = "PhotoImage",
  OtherVideo = "OtherVideo",
  OtherVideoData = "OtherVideoData",
}

export interface ErrorDTO {
  message: string;
  metadata: DictionaryOfStringAndObject;
  reasons: ListOfErrorDTO;
}

/** the dto used to send an error response to the client */
export interface ErrorResponse {
  errors: Record<string, string[]>;
  /**
   * the message for the error response
   * @default "One or more errors occurred!"
   */
  message: string;
  /**
   * the http status code sent to the client. default is 400.
   * @format int32
   * @default 400
   */
  statusCode: number;
}

export interface FileSystemDTO {
  current?: FileSystemModelDTO | null;
  directories: ListOfFileSystemModelDTO;
  files: ListOfFileSystemModelDTO;
  parent: string;
}

export enum FileSystemEntityType {
  Parent = "Parent",
  Drive = "Drive",
  Folder = "Folder",
  File = "File",
}

export interface FileSystemModelDTO {
  extension: string;
  hasReadPermission: boolean;
  hasWritePermission: boolean;
  /** @format date-time */
  lastModified?: string | null;
  name: string;
  path: string;
  /** @format int64 */
  size: number;
  type: FileSystemEntityType;
}

export interface FolderPathDTO {
  directory: string;
  displayName: string;
  folderType: FolderType;
  /** @format int32 */
  id: number;
  isDefault: boolean;
  isValid: boolean;
  mediaType: PlexMediaType;
}

export enum FolderType {
  None = "None",
  DownloadFolder = "DownloadFolder",
  MovieFolder = "MovieFolder",
  TvShowFolder = "TvShowFolder",
  MusicFolder = "MusicFolder",
  PhotosFolder = "PhotosFolder",
  OtherVideosFolder = "OtherVideosFolder",
  GamesVideosFolder = "GamesVideosFolder",
  Unknown = "Unknown",
}

export interface GeneralSettingsDTO {
  /** @format int32 */
  activeAccountId: number;
  disableAnimatedBackground: boolean;
  firstTimeSetup: boolean;
  hasAgreedToDisclaimer: boolean;
  hasBeenInvitedToDiscord: boolean;
  hideMediaFromOfflineServers: boolean;
  hideMediaFromOwnedServers: boolean;
  useLowQualityPosterImages: boolean;
}

export interface GeneratePlexTokenResponse {
  isUnAuthorized: boolean;
  needsVerificationCode: boolean;
  plexAuthToken: string;
}

export interface IError {
  message: string | null;
  metadata: DictionaryOfStringAndObject;
  reasons: ListOfIError;
}

export type IReadOnlyListOfErrorDTO = ErrorDTO[];

export type IReadOnlyListOfIError = IError[];

export type IReadOnlyListOfInt32 = number[];

export type IReadOnlyListOfLibrarySyncProgressItemDTO =
  LibrarySyncProgressItemDTO[];

export type IReadOnlyListOfReleaseNoteDTO = ReleaseNoteDTO[];

export type IReadOnlyListOfSortType = SortType[];

export type IReadOnlyListOfSuccessDTO = SuccessDTO[];

export interface InspectPlexServerJobUpdateDTO {
  plexServerIds: ListOfInt32;
}

export interface IntegrationIdentity {
  /** @format uuid */
  id: string;
  type: IntegrationType;
}

export enum IntegrationProvisioningState {
  Unconfigured = "Unconfigured",
  ChangesPending = "ChangesPending",
  Configured = "Configured",
}

export interface IntegrationSetupProgressDTO {
  error?: string | null;
  /** @format uuid */
  integrationId: string;
  isRunning: boolean;
  isSuccess: boolean;
  stage: IntegrationSetupProgressStage;
}

export enum IntegrationSetupProgressStage {
  Connecting = "Connecting",
  DownloadClient = "DownloadClient",
  Indexer = "Indexer",
  Validation = "Validation",
  Done = "Done",
}

export interface IntegrationSummary {
  baseUrl: string;
  category: string;
  /** @format int32 */
  downloadFolderId: number;
  /** @format int32 */
  externalDownloadClientId?: number | null;
  /** @format int32 */
  externalIndexerId?: number | null;
  /** @format uuid */
  id: string;
  lastConnectionTestErrorMessage?: string | null;
  /** @format int32 */
  lastConnectionTestHttpStatusCode?: number | null;
  lastConnectionTestStatus: TestConnectionStatus;
  /** @format date-time */
  lastConnectionTestedAt?: string | null;
  name: string;
  provisioningState: IntegrationProvisioningState;
  type: IntegrationType;
}

export enum IntegrationType {
  Sonarr = "Sonarr",
  Radarr = "Radarr",
}

export enum JobStatus {
  Started = "Started",
  Completed = "Completed",
  Cancelled = "Cancelled",
  Failed = "Failed",
  Queued = "Queued",
}

export interface JobStatusUpdateDTO {
  id: string;
  /** @format date-time */
  jobStartTime: string;
  jobType: JobTypes;
  jsonString: string;
  status: JobStatus;
}

export enum JobTypes {
  None = "None",
  CheckAllConnectionsStatusByPlexServerJob = "CheckAllConnectionsStatusByPlexServerJob",
  DownloadJob = "DownloadJob",
  MoveDownloadFileJob = "MoveDownloadFileJob",
  InspectPlexServerJob = "InspectPlexServerJob",
  LibrarySyncJob = "LibrarySyncJob",
  MetadataSyncJob = "MetadataSyncJob",
  CheckForUpdateJob = "CheckForUpdateJob",
  CheckPlexLibrariesForUpdatesJob = "CheckPlexLibrariesForUpdatesJob",
  LibraryComparisonJob = "LibraryComparisonJob",
  RefreshPlexAccountAccessJob = "RefreshPlexAccountAccessJob",
  MediaOverviewSnapshotJob = "MediaOverviewSnapshotJob",
  OptimizeDatabaseJob = "OptimizeDatabaseJob",
  UpdateScheduledDownloadLimitsJob = "UpdateScheduledDownloadLimitsJob",
  Unknown = "Unknown",
}

export interface LanguageSettingsDTO {
  language: string;
}

export interface LibraryComparisonCompletedDTO {
  affectedLibraryIds: IReadOnlyListOfInt32;
  /** @format date-time */
  completedAt: string;
  mediaType: PlexMediaType;
}

export interface LibrarySyncJobQueueDTO {
  /** @format date-time */
  completedAt?: string | null;
  /** @format date-time */
  createdAt: string;
  errorMessage?: string | null;
  isServerOffline: boolean;
  /** @format int32 */
  plexLibraryId: number;
  /** @format int32 */
  plexServerId: number;
  /** @format int32 */
  priority: number;
  /** @format date-time */
  startedAt?: string | null;
  status: LibrarySyncJobStatus;
}

export enum LibrarySyncJobStatus {
  Unknown = "Unknown",
  Queued = "Queued",
  Processing = "Processing",
  Completed = "Completed",
  Failed = "Failed",
  Cancelled = "Cancelled",
}

export interface LibrarySyncProgressDTO {
  errors: IReadOnlyListOfIError;
  isComplete: boolean;
  items: IReadOnlyListOfLibrarySyncProgressItemDTO;
  /** @format double */
  percentage: number;
  /** @format int32 */
  plexLibraryId: number;
  /** @format int32 */
  received: number;
  /** @pattern ^-?(\d+\.)?\d{2}:\d{2}:\d{2}(\.\d{1,7})?$ */
  timeRemaining: string;
  /** @format date-time */
  timeStamp: string;
  /** @format int32 */
  total: number;
}

export interface LibrarySyncProgressItemDTO {
  isComplete: boolean;
  mediaType: PlexMediaType;
  /** @format double */
  percentage: number;
  /** @format int32 */
  received: number;
  /** @pattern ^-?(\d+\.)?\d{2}:\d{2}:\d{2}(\.\d{1,7})?$ */
  timeRemaining: string;
  /** @format int32 */
  total: number;
}

export type ListOfDownloadMediaDTO = DownloadMediaDTO[];

/** @minItems 1 */
export type ListOfDownloadMediaDTOOpDF26594E59F4 = DownloadMediaDTO[];

export type ListOfDownloadPatchEntryMessagePackDTO =
  DownloadPatchEntryMessagePackDTO[];

export type ListOfDownloadPreviewDTO = DownloadPreviewDTO[];

export type ListOfDownloadProgressDTO = DownloadProgressDTO[];

export type ListOfDownloadProgressMessagePackDTO =
  DownloadProgressMessagePackDTO[];

export type ListOfDownloadTaskDTO = DownloadTaskDTO[];

export type ListOfErrorDTO = ErrorDTO[];

export type ListOfFileSystemModelDTO = FileSystemModelDTO[];

export type ListOfGuid = string[];

export type ListOfIError = IError[];

export type ListOfInt32 = number[];

export type ListOfMediaNavigationIndexDTO = MediaNavigationIndexDTO[];

export type ListOfPhotoAlbumBrowseDTO = PhotoAlbumBrowseDTO[];

export type ListOfPhotoAlbumSummaryDTO = PhotoAlbumSummaryDTO[];

export type ListOfPhotoAssetBrowseDTO = PhotoAssetBrowseDTO[];

export type ListOfPhotoFolderBreadcrumbDTO = PhotoFolderBreadcrumbDTO[];

export type ListOfPhotoFolderBrowseDTO = PhotoFolderBrowseDTO[];

export type ListOfPlexCountryDTO = PlexCountryDTO[];

export type ListOfPlexGenreDTO = PlexGenreDTO[];

export type ListOfPlexLibraryAccessCurrentStateDTO =
  PlexLibraryAccessCurrentStateDTO[];

export type ListOfPlexLibraryAccessCurrentStateLibraryDTO =
  PlexLibraryAccessCurrentStateLibraryDTO[];

export type ListOfPlexLibraryAccessRapportDTO = PlexLibraryAccessRapportDTO[];

export type ListOfPlexLibraryAccessTimelineEventDTO =
  PlexLibraryAccessTimelineEventDTO[];

export type ListOfPlexMediaComparisonDetailsRowDTO =
  PlexMediaComparisonDetailsRowDTO[];

export type ListOfPlexMediaDTO = PlexMediaDTO[];

export type ListOfPlexMediaDataDTO = PlexMediaDataDTO[];

export type ListOfPlexMediaQualityDTO = PlexMediaQualityDTO[];

export type ListOfPlexMediaSlimDTO = PlexMediaSlimDTO[];

export type ListOfPlexQualityDTO = PlexQualityDTO[];

export type ListOfPlexRoleDTO = PlexRoleDTO[];

export type ListOfPlexServerAccessRapportDTO = PlexServerAccessRapportDTO[];

export type ListOfPlexServerSettingItemModule = PlexServerSettingItemModule[];

export type ListOfString = string[];

export interface LiveLogEventDTO {
  exception?: string | null;
  level: LogSeverity;
  message: string;
  /** @format int64 */
  sequence: number;
  sourceContext?: string | null;
  /** @format date-time */
  timestamp: string;
}

export enum LogSeverity {
  None = "None",
  Verbose = "Verbose",
  Debug = "Debug",
  Information = "Information",
  Success = "Success",
  Warning = "Warning",
  Error = "Error",
  Fatal = "Fatal",
}

export interface MediaNavigationIndexDTO {
  /** @format int32 */
  index: number;
  label: string;
}

export interface MediaQueryFilterDTO {
  distinct?: boolean | null;
  filter?: string | null;
  /** @default false */
  filterOfflineMedia: boolean;
  /** @default false */
  filterOwnedMedia: boolean;
  groupBy?: string | null;
  having?: string | null;
  includeCount?: boolean | null;
  includes?: string | null;
  mediaType: PlexMediaType;
  mode?: string | null;
  /** @format int32 */
  page?: number | null;
  /** @format int32 */
  pageSize?: number | null;
  /** @format int32 */
  plexLibraryId: number;
  query?: string | null;
  select?: string | null;
  sort?: string | null;
}

export enum MessageTypes {
  LibraryProgress = "LibraryProgress",
  DownloadTaskUpdate = "DownloadTaskUpdate",
  ServerDownloadProgress = "ServerDownloadProgress",
  DownloadPatch = "DownloadPatch",
  ServerConnectionCheckStatusProgress = "ServerConnectionCheckStatusProgress",
  MoveDownloadFileProgress = "MoveDownloadFileProgress",
  Notification = "Notification",
  JobStatusUpdate = "JobStatusUpdate",
  RefreshNotification = "RefreshNotification",
  AppUpdateDownloadProgress = "AppUpdateDownloadProgress",
  LogEvent = "LogEvent",
  IntegrationSetupProgress = "IntegrationSetupProgress",
}

export interface MoveDownloadFileJobUpdateDTO {
  id: DownloadTaskKey;
}

export interface NetworkSettingsDTO {
  allowedProxyIps: ListOfString;
  basePath: string;
  forwardedHostHeader: string;
  forwardedPathHeader: string;
  reverseProxyUrl: string;
  trustProxyHeaders: boolean;
}

export interface NotificationDTO {
  /** @format date-time */
  createdAt: string;
  hidden: boolean;
  /** @format int32 */
  id: number;
  level: NotificationLevel;
  message: string;
}

export enum NotificationLevel {
  None = "None",
  Verbose = "Verbose",
  Debug = "Debug",
  Information = "Information",
  Success = "Success",
  Warning = "Warning",
  Error = "Error",
  Fatal = "Fatal",
}

export interface PhotoAlbumBrowseDTO {
  /** @format int64 */
  assetBytes: number;
  /** @format int32 */
  assetCount: number;
  hasThumbnail: boolean;
  /** @format int32 */
  id: number;
  /** @format int32 */
  plexApiMetaDataKey: number;
  /** @format int32 */
  plexApiRatingKey: number;
  /** @format int32 */
  plexLibraryId: number;
  plexLibraryTitle: string;
  /** @format int32 */
  plexServerId: number;
  plexServerName: string;
  title: string;
}

export interface PhotoAlbumPageDTO {
  items: ListOfPhotoAlbumBrowseDTO;
  /** @format int32 */
  page: number;
  /** @format int32 */
  pageSize: number;
  /** @format int64 */
  totalAssetBytes: number;
  /** @format int32 */
  totalCount: number;
}

export interface PhotoAlbumSummaryDTO {
  hasThumbnail: boolean;
  /** @format int32 */
  id: number;
  /** @format int32 */
  plexApiMetaDataKey: number;
  /** @format int32 */
  plexApiRatingKey: number;
  /** @format int32 */
  plexLibraryId: number;
  plexLibraryTitle: string;
  /** @format int32 */
  plexServerId: number;
  plexServerName: string;
  title: string;
}

export interface PhotoAlbumSummaryPageDTO {
  items: ListOfPhotoAlbumSummaryDTO;
  /** @format int32 */
  page: number;
  /** @format int32 */
  pageSize: number;
  /** @format int32 */
  totalCount: number;
}

export interface PhotoAssetBrowseDTO {
  assetKind: PhotoAssetKind;
  container: string;
  /** @format int32 */
  duration?: number | null;
  hasThumbnail: boolean;
  /** @format int32 */
  height?: number | null;
  /** @format int32 */
  id: number;
  originalFilename: string;
  /** @format int32 */
  plexApiMetaDataKey: number;
  /** @format int32 */
  plexApiRatingKey: number;
  /** @format int32 */
  plexLibraryId: number;
  plexLibraryTitle: string;
  /** @format int32 */
  plexServerId: number;
  plexServerName: string;
  relativeFolderPath?: string | null;
  /** @format int64 */
  size: number;
  sourceRootTitle?: string | null;
  title: string;
  /** @format int32 */
  width?: number | null;
}

export interface PhotoAssetInspectionDTO {
  /** @format date-time */
  addedAt: string;
  albums: PhotoAlbumSummaryPageDTO;
  assetKind: PhotoAssetKind;
  audioCodec?: string | null;
  breadcrumbs: ListOfPhotoFolderBreadcrumbDTO;
  container: string;
  /** @format int32 */
  duration?: number | null;
  hasThumbnail: boolean;
  /** @format int32 */
  height?: number | null;
  /** @format int32 */
  id: number;
  originalFilename: string;
  /** @format date-time */
  originallyAvailableAt?: string | null;
  /** @format int32 */
  plexApiMetaDataKey: number;
  /** @format int32 */
  plexApiRatingKey: number;
  /** @format int32 */
  plexLibraryId: number;
  plexLibraryTitle: string;
  /** @format int32 */
  plexServerId: number;
  plexServerName: string;
  relativeFolderPath?: string | null;
  /** @format int64 */
  size: number;
  sourceRootTitle?: string | null;
  summary?: string | null;
  title: string;
  /** @format date-time */
  updatedAt?: string | null;
  videoCodec?: string | null;
  /** @format int32 */
  width?: number | null;
}

export enum PhotoAssetKind {
  Image = "Image",
  VideoClip = "VideoClip",
}

export interface PhotoAssetPageDTO {
  items: ListOfPhotoAssetBrowseDTO;
  /** @format int32 */
  page: number;
  /** @format int32 */
  pageSize: number;
  /** @format int64 */
  totalBytes: number;
  /** @format int32 */
  totalCount: number;
}

export interface PhotoFolderBreadcrumbDTO {
  /** @format int32 */
  id: number;
  relativePath: string;
  title: string;
}

export interface PhotoFolderBrowseDTO {
  /** @format int32 */
  id: number;
  isActive?: boolean | null;
  isRoot: boolean;
  /** @format int32 */
  parentId?: number | null;
  /** @format int32 */
  plexLibraryId: number;
  /** @format int32 */
  plexServerId: number;
  /** @format int64 */
  recursiveAssetBytes: number;
  /** @format int32 */
  recursiveAssetCount: number;
  relativePath: string;
  title: string;
}

export interface PhotoFolderChildrenDTO {
  assets: PhotoAssetPageDTO;
  breadcrumbs: ListOfPhotoFolderBreadcrumbDTO;
  folder: PhotoFolderBrowseDTO;
  folders: PhotoFolderPageDTO;
}

export interface PhotoFolderPageDTO {
  items: ListOfPhotoFolderBrowseDTO;
  /** @format int32 */
  page: number;
  /** @format int32 */
  pageSize: number;
  /** @format int32 */
  totalCount: number;
}

export enum PhotoImageRepresentation {
  Original = "original",
  Converted = "converted",
}

export enum PhotoItemKind {
  Folder = "Folder",
  Album = "Album",
  Asset = "Asset",
}

export enum PlexAccessState {
  Unknown = "Unknown",
  Revoked = "Revoked",
  Updated = "Updated",
  Granted = "Granted",
}

export interface PlexAccountDTO {
  is2Fa: boolean;
  authenticationToken: string;
  clientId: string;
  customAuthenticationToken: string;
  displayName: string;
  email: string;
  hasPassword: boolean;
  /** @format int32 */
  id: number;
  isEnabled: boolean;
  isMain: boolean;
  isValidated: boolean;
  password: string;
  /** @format int64 */
  plexId: number;
  plexLibraryAccess: ListOfInt32;
  plexServerAccess: ListOfInt32;
  title: string;
  username: string;
  uuid: string;
  /** @format date-time */
  validatedAt?: string | null;
  verificationCode: string;
}

export enum PlexConnectionTypes {
  Local = "Local",
  Public = "Public",
  PlexRelay = "PlexRelay",
  Unknown = "Unknown",
}

export interface PlexCountryDTO {
  /** @format int32 */
  id: number;
  name: string;
}

export interface PlexGenreDTO {
  /** @format int32 */
  id: number;
  name: string;
}

export interface PlexLibraryAccessCurrentStateDTO {
  libraries: ListOfPlexLibraryAccessCurrentStateLibraryDTO;
  /** @format int32 */
  plexServerId?: number | null;
  plexServerName?: string | null;
}

export interface PlexLibraryAccessCurrentStateLibraryDTO {
  /** @format date-time */
  grantedAt: string;
  /** @format date-time */
  lastChangedAt: string;
  /** @format int32 */
  plexAccountId: number;
  plexAccountName: string;
  /** @format int32 */
  plexLibraryId?: number | null;
  plexLibraryName?: string | null;
  /** @format int32 */
  plexServerId?: number | null;
  plexServerName?: string | null;
}

export interface PlexLibraryAccessRapportDTO {
  /** @format int32 */
  plexLibraryId: number;
  plexLibraryName: string;
  /** @format int32 */
  plexServerId: number;
  state: PlexAccessState;
}

export interface PlexLibraryAccessTimelineDTO {
  currentState: ListOfPlexLibraryAccessCurrentStateDTO;
  events: ListOfPlexLibraryAccessTimelineEventDTO;
}

export interface PlexLibraryAccessTimelineEventDTO {
  /** @format date-time */
  createdAt: string;
  /** @format int32 */
  id: number;
  /** @format int32 */
  plexAccountId: number;
  plexAccountName: string;
  /** @format int32 */
  plexLibraryId?: number | null;
  plexLibraryName?: string | null;
  /** @format int32 */
  plexServerId?: number | null;
  plexServerName?: string | null;
  /** @format uuid */
  refreshRunId: string;
  state: PlexAccessState;
}

export interface PlexLibraryDTO {
  /** @format int32 */
  count: number;
  /** @format date-time */
  createdAt?: string | null;
  defaultDestination?: FolderPathDTO | null;
  /** @format int32 */
  defaultDestinationId: number;
  /** @format int32 */
  episodeCount: number;
  /** @format int32 */
  id: number;
  isEnabled: boolean;
  key: string;
  /** @format int64 */
  mediaSize: number;
  /** @format int32 */
  musicAlbumCount: number;
  /** @format int32 */
  musicArtistCount: number;
  /** @format int32 */
  musicTrackCount: number;
  /** @format int32 */
  otherVideoCount: number;
  outdated: boolean;
  /** @format int32 */
  photoAlbumCount: number;
  /** @format int32 */
  photoClipCount: number;
  /** @format int32 */
  photoImageCount: number;
  /** @format int32 */
  plexServerId: number;
  /** @format date-time */
  scannedAt?: string | null;
  /** @format int32 */
  seasonCount: number;
  /** @format date-time */
  syncedAt?: string | null;
  title: string;
  type: PlexMediaType;
  /** @format date-time */
  updatedAt?: string | null;
  uuid: string;
}

export interface PlexMediaComparisonDetailsDTO {
  /** @format int32 */
  plexMediaId: number;
  rows: ListOfPlexMediaComparisonDetailsRowDTO;
  state: PlexMediaComparisonState;
  type: PlexMediaType;
}

export interface PlexMediaComparisonDetailsRowDTO {
  children: ListOfPlexMediaComparisonDetailsRowDTO;
  ownedAudioQuality: AudioQuality;
  /** @format int32 */
  ownedPlexMediaDataId?: number | null;
  ownedQuality: VideoQuality;
  /** @format int32 */
  plexLibraryId: number;
  /** @format int32 */
  plexMediaId: number;
  /** @format int32 */
  plexServerId: number;
  remoteAudioQuality: AudioQuality;
  /** @format int32 */
  remotePlexMediaDataId?: number | null;
  remoteQuality: VideoQuality;
  state: PlexMediaComparisonState;
  title: string;
  type: PlexMediaType;
}

export enum PlexMediaComparisonState {
  NotCompared = "NotCompared",
  Owned = "Owned",
  Pending = "Pending",
  Missing = "Missing",
  HigherQuality = "HigherQuality",
  Partial = "Partial",
  PartialAndHigherQuality = "PartialAndHigherQuality",
  Unknown = "Unknown",
}

export interface PlexMediaDTO {
  /** @format date-time */
  addedAt: string;
  /** @format int32 */
  childCount: number;
  children: ListOfPlexMediaDTO;
  /** @format int32 */
  comparisonId: number;
  contentRating?: string | null;
  /** @format int32 */
  discNumber?: number | null;
  /** @format int32 */
  duration: number;
  genres: ListOfPlexGenreDTO;
  /** @format int32 */
  grandChildCount: number;
  hasArt: boolean;
  hasTheme: boolean;
  hasThumb: boolean;
  /** @format int32 */
  id: number;
  mediaData: ListOfPlexMediaDataDTO;
  /** @format int64 */
  mediaSize: number;
  /** @format date-time */
  originallyAvailableAt?: string | null;
  /** @format int32 */
  parentId?: number | null;
  /** @format int32 */
  plexApiMetaDataKey: number;
  /** @format int32 */
  plexApiRatingKey: number;
  /** @format int32 */
  plexLibraryId: number;
  /** @format int32 */
  plexServerId: number;
  qualities: ListOfPlexMediaQualityDTO;
  /** @format double */
  rating: number;
  recordLabel?: string | null;
  searchTitle: string;
  /** @format int32 */
  sortIndex: number;
  studio: string;
  summary: string;
  title: string;
  /** @format int32 */
  trackNumber?: number | null;
  /** @format int32 */
  tvShowId: number;
  /** @format int32 */
  tvShowSeasonId: number;
  type: PlexMediaType;
  /** @format date-time */
  updatedAt?: string | null;
  /** @format int32 */
  year: number;
}

export interface PlexMediaDataDTO {
  audioCodec: string;
  audioQuality: AudioQuality;
  /** @format int64 */
  duration: number;
  fileName: string;
  /** @format int32 */
  id: number;
  /** @format int32 */
  plexApiMediaId: number;
  /** @format int32 */
  plexApiPartId: number;
  /** @format int64 */
  size: number;
  videoCodec: string;
  videoQuality: VideoQuality;
}

export interface PlexMediaFilterMetadataDTO {
  countries: ListOfInt32;
  genres: ListOfInt32;
  qualities: ListOfInt32;
  roles: ListOfInt32;
}

export interface PlexMediaMetadataDTO {
  countries: ListOfPlexCountryDTO;
  /** @format int32 */
  countryCount: number;
  /** @format int32 */
  genreCount: number;
  genres: ListOfPlexGenreDTO;
  /** @format int32 */
  mediaCount: number;
  qualities: ListOfPlexQualityDTO;
  /** @format int32 */
  qualityCount: number;
  /** @format int32 */
  roleCount: number;
  roles: ListOfPlexRoleDTO;
  supportedSorts: IReadOnlyListOfSortType;
}

export interface PlexMediaQualityDTO {
  audioQuality: AudioQuality;
  /** @format int32 */
  dataId: number;
  mediaDataType: PlexMediaType;
  /** @format int32 */
  mediaId: number;
  videoQuality: VideoQuality;
}

export interface PlexMediaSlimDTO {
  /** @format date-time */
  addedAt: string;
  /** @format int32 */
  childCount: number;
  /** @format int32 */
  comparisonId: number;
  /** @format int32 */
  duration: number;
  /** @format int32 */
  grandChildCount: number;
  hasThumb: boolean;
  /** @format int32 */
  id: number;
  /** @format int64 */
  mediaSize: number;
  /** @format int32 */
  parentId?: number | null;
  /** @format int32 */
  plexApiMetaDataKey: number;
  /** @format int32 */
  plexApiRatingKey: number;
  /** @format int32 */
  plexLibraryId: number;
  /** @format int32 */
  plexServerId: number;
  qualities: ListOfPlexMediaQualityDTO;
  searchTitle: string;
  /** @format int32 */
  sortIndex: number;
  title: string;
  type: PlexMediaType;
  /** @format date-time */
  updatedAt?: string | null;
  /** @format int32 */
  year: number;
}

export interface PlexMediaStatisticsDTO {
  countries: ListOfInt32;
  /** @format int32 */
  episodeCount: number;
  genres: ListOfInt32;
  /** @format int32 */
  mediaCount: number;
  mediaList: ListOfPlexMediaSlimDTO;
  /** @format int64 */
  mediaSize: number;
  /** @format int32 */
  movieCount: number;
  navigationIndexes: ListOfMediaNavigationIndexDTO;
  /** @format int32 */
  page: number;
  /** @format int32 */
  pageSize: number;
  qualities: ListOfInt32;
  queryHash: string;
  roles: ListOfInt32;
  /** @format int32 */
  seasonCount: number;
  /** @format int32 */
  totalCount: number;
  /** @format int32 */
  totalEpisodeCount: number;
  /** @format int64 */
  totalMediaSize: number;
  /** @format int32 */
  totalMovieCount: number;
  /** @format int32 */
  totalSeasonCount: number;
  /** @format int32 */
  totalTvShowCount: number;
  /** @format int32 */
  tvShowCount: number;
}

export enum PlexMediaType {
  None = "None",
  Movie = "Movie",
  TvShow = "TvShow",
  Season = "Season",
  Episode = "Episode",
  MusicArtist = "MusicArtist",
  MusicAlbum = "MusicAlbum",
  MusicTrack = "MusicTrack",
  PhotoAlbum = "PhotoAlbum",
  PhotoImage = "PhotoImage",
  OtherVideos = "OtherVideos",
  Games = "Games",
  Unknown = "Unknown",
}

export interface PlexQualityDTO {
  /** @format int32 */
  count: number;
  /** @format int32 */
  id: number;
  name: string;
  quality: VideoQuality;
}

export interface PlexRoleDTO {
  /** @format int32 */
  id: number;
  name: string;
}

export interface PlexServerAccessRapportDTO {
  isServerOffline: boolean;
  libraryAccess: ListOfPlexLibraryAccessRapportDTO;
  /** @format int32 */
  plexServerId: number;
  plexServerName: string;
  state: PlexAccessState;
}

export interface PlexServerConnectionDTO {
  iPv4: boolean;
  iPv6: boolean;
  address: string;
  chosenConnection: boolean;
  /** @format int32 */
  id: number;
  isCustom: boolean;
  isPlexTvConnection: boolean;
  latestConnectionStatus?: PlexServerStatusDTO | null;
  local: boolean;
  /** @format int32 */
  plexServerId: number;
  /** @format int32 */
  port: number;
  protocol: string;
  relay: boolean;
  type: PlexConnectionTypes;
  url: string;
}

export interface PlexServerDTO {
  /** @format date-time */
  createdAt: string;
  device: string;
  dnsRebindingProtection: boolean;
  httpsRequired: boolean;
  /** @format int32 */
  id: number;
  isDownloadsPausedByUser: boolean;
  isEnabled: boolean;
  /** @format date-time */
  lastSeenAt: string;
  machineIdentifier: string;
  name: string;
  natLoopbackSupported: boolean;
  owned: boolean;
  /** @format int64 */
  ownerId: number;
  platform: string;
  platformVersion: string;
  plexServerOwnerUsername: string;
  /** @format int32 */
  preferredConnectionId: number;
  presence: boolean;
  product: string;
  productVersion: string;
  provides: string;
  publicAddress: string;
  publicAddressMatches: boolean;
  relay: boolean;
  synced: boolean;
}

export interface PlexServerSettingItemModule {
  allowStreamDownloader: boolean;
  /** @format int32 */
  downloadSpeedLimit: number;
  machineIdentifier: string;
  plexServerName: string;
}

export interface PlexServerStatusDTO {
  /** @format int32 */
  id: number;
  isSuccessful: boolean;
  /** @format date-time */
  lastChecked: string;
  /** @format int32 */
  plexServerConnectionId: number;
  /** @format int32 */
  plexServerId: number;
  /** @format int32 */
  statusCode: number;
  statusMessage: string;
}

export interface RadarrIntegrationDTO {
  apiKey: string;
  category: string;
  /** @format int32 */
  downloadFolderId: number;
  /** @format uuid */
  id: string;
  lastConnectionTestErrorMessage?: string | null;
  /** @format int32 */
  lastConnectionTestHttpStatusCode?: number | null;
  lastConnectionTestStatus: TestConnectionStatus;
  /** @format date-time */
  lastConnectionTestedAt?: string | null;
  name: string;
  provisioningState: IntegrationProvisioningState;
  url: string;
}

export enum RefreshDataType {
  PlexAccount = "PlexAccount",
  PlexServer = "PlexServer",
  PlexLibrary = "PlexLibrary",
  PlexLibrarySyncStatus = "PlexLibrarySyncStatus",
  PlexServerConnection = "PlexServerConnection",
  DownloadTasks = "DownloadTasks",
  UpdateAvailable = "UpdateAvailable",
}

export interface RefreshLibraryMediaEndpointRequest {
  forceLibrarySync: boolean;
  forceMediaRefresh: boolean;
}

export interface RefreshPlexAccountAccessRapportDTO {
  access: ListOfPlexServerAccessRapportDTO;
  /** @format int32 */
  plexAccountId: number;
  plexAccountName: string;
}

export interface ReleaseNoteDTO {
  isDevRelease: boolean;
  notes: string;
  /** @format date-time */
  releaseDate: string;
  version: string;
}

export interface ResultDTOOfAppCredentialsDTO {
  errors: IReadOnlyListOfErrorDTO;
  isSuccess: boolean;
  /** @format int32 */
  statusCode: number;
  successes: IReadOnlyListOfSuccessDTO;
  value?: AppCredentialsDTO | null;
}

export interface ResultDTOOfAppUpdateCheckDTO {
  errors: IReadOnlyListOfErrorDTO;
  isSuccess: boolean;
  /** @format int32 */
  statusCode: number;
  successes: IReadOnlyListOfSuccessDTO;
  value?: AppUpdateCheckDTO | null;
}

export interface ResultDTOOfBoolean {
  errors: IReadOnlyListOfErrorDTO;
  isSuccess: boolean;
  /** @format int32 */
  statusCode: number;
  successes: IReadOnlyListOfSuccessDTO;
  value: boolean;
}

export interface ResultDTOOfCountResponseDTO {
  errors: IReadOnlyListOfErrorDTO;
  isSuccess: boolean;
  /** @format int32 */
  statusCode: number;
  successes: IReadOnlyListOfSuccessDTO;
  value?: CountResponseDTO | null;
}

export interface ResultDTOOfDownloadPreviewContainerDTO {
  errors: IReadOnlyListOfErrorDTO;
  isSuccess: boolean;
  /** @format int32 */
  statusCode: number;
  successes: IReadOnlyListOfSuccessDTO;
  value?: DownloadPreviewContainerDTO | null;
}

export interface ResultDTOOfDownloadTaskCreationReportDTO {
  errors: IReadOnlyListOfErrorDTO;
  isSuccess: boolean;
  /** @format int32 */
  statusCode: number;
  successes: IReadOnlyListOfSuccessDTO;
  value?: DownloadTaskCreationReportDTO | null;
}

export interface ResultDTOOfDownloadTaskDTO {
  errors: IReadOnlyListOfErrorDTO;
  isSuccess: boolean;
  /** @format int32 */
  statusCode: number;
  successes: IReadOnlyListOfSuccessDTO;
  value?: DownloadTaskDTO | null;
}

export interface ResultDTOOfFileSystemDTO {
  errors: IReadOnlyListOfErrorDTO;
  isSuccess: boolean;
  /** @format int32 */
  statusCode: number;
  successes: IReadOnlyListOfSuccessDTO;
  value?: FileSystemDTO | null;
}

export interface ResultDTOOfFolderPathDTO {
  errors: IReadOnlyListOfErrorDTO;
  isSuccess: boolean;
  /** @format int32 */
  statusCode: number;
  successes: IReadOnlyListOfSuccessDTO;
  value?: FolderPathDTO | null;
}

export interface ResultDTOOfGeneratePlexTokenResponse {
  errors: IReadOnlyListOfErrorDTO;
  isSuccess: boolean;
  /** @format int32 */
  statusCode: number;
  successes: IReadOnlyListOfSuccessDTO;
  value?: GeneratePlexTokenResponse | null;
}

export interface ResultDTOOfInt32 {
  errors: IReadOnlyListOfErrorDTO;
  isSuccess: boolean;
  /** @format int32 */
  statusCode: number;
  successes: IReadOnlyListOfSuccessDTO;
  /** @format int32 */
  value: number;
}

export interface ResultDTOOfLibrarySyncJobQueueDTO {
  errors: IReadOnlyListOfErrorDTO;
  isSuccess: boolean;
  /** @format int32 */
  statusCode: number;
  successes: IReadOnlyListOfSuccessDTO;
  value?: LibrarySyncJobQueueDTO | null;
}

export interface ResultDTOOfListOfDownloadTaskLogDTO {
  errors: IReadOnlyListOfErrorDTO;
  isSuccess: boolean;
  /** @format int32 */
  statusCode: number;
  successes: IReadOnlyListOfSuccessDTO;
  value?: DownloadTaskLogDTO[] | null;
}

export interface ResultDTOOfListOfFolderPathDTO {
  errors: IReadOnlyListOfErrorDTO;
  isSuccess: boolean;
  /** @format int32 */
  statusCode: number;
  successes: IReadOnlyListOfSuccessDTO;
  value?: FolderPathDTO[] | null;
}

export interface ResultDTOOfListOfIntegrationSummary {
  errors: IReadOnlyListOfErrorDTO;
  isSuccess: boolean;
  /** @format int32 */
  statusCode: number;
  successes: IReadOnlyListOfSuccessDTO;
  value?: IntegrationSummary[] | null;
}

export interface ResultDTOOfListOfJobStatusUpdateDTO {
  errors: IReadOnlyListOfErrorDTO;
  isSuccess: boolean;
  /** @format int32 */
  statusCode: number;
  successes: IReadOnlyListOfSuccessDTO;
  value?: JobStatusUpdateDTO[] | null;
}

export interface ResultDTOOfListOfLibrarySyncJobQueueDTO {
  errors: IReadOnlyListOfErrorDTO;
  isSuccess: boolean;
  /** @format int32 */
  statusCode: number;
  successes: IReadOnlyListOfSuccessDTO;
  value?: LibrarySyncJobQueueDTO[] | null;
}

export interface ResultDTOOfListOfLiveLogEventDTO {
  errors: IReadOnlyListOfErrorDTO;
  isSuccess: boolean;
  /** @format int32 */
  statusCode: number;
  successes: IReadOnlyListOfSuccessDTO;
  value?: LiveLogEventDTO[] | null;
}

export interface ResultDTOOfListOfNotificationDTO {
  errors: IReadOnlyListOfErrorDTO;
  isSuccess: boolean;
  /** @format int32 */
  statusCode: number;
  successes: IReadOnlyListOfSuccessDTO;
  value?: NotificationDTO[] | null;
}

export interface ResultDTOOfListOfPlexAccountDTO {
  errors: IReadOnlyListOfErrorDTO;
  isSuccess: boolean;
  /** @format int32 */
  statusCode: number;
  successes: IReadOnlyListOfSuccessDTO;
  value?: PlexAccountDTO[] | null;
}

export interface ResultDTOOfListOfPlexLibraryDTO {
  errors: IReadOnlyListOfErrorDTO;
  isSuccess: boolean;
  /** @format int32 */
  statusCode: number;
  successes: IReadOnlyListOfSuccessDTO;
  value?: PlexLibraryDTO[] | null;
}

export interface ResultDTOOfListOfPlexServerConnectionDTO {
  errors: IReadOnlyListOfErrorDTO;
  isSuccess: boolean;
  /** @format int32 */
  statusCode: number;
  successes: IReadOnlyListOfSuccessDTO;
  value?: PlexServerConnectionDTO[] | null;
}

export interface ResultDTOOfListOfPlexServerDTO {
  errors: IReadOnlyListOfErrorDTO;
  isSuccess: boolean;
  /** @format int32 */
  statusCode: number;
  successes: IReadOnlyListOfSuccessDTO;
  value?: PlexServerDTO[] | null;
}

export interface ResultDTOOfListOfPlexServerStatusDTO {
  errors: IReadOnlyListOfErrorDTO;
  isSuccess: boolean;
  /** @format int32 */
  statusCode: number;
  successes: IReadOnlyListOfSuccessDTO;
  value?: PlexServerStatusDTO[] | null;
}

export interface ResultDTOOfListOfRefreshPlexAccountAccessRapportDTO {
  errors: IReadOnlyListOfErrorDTO;
  isSuccess: boolean;
  /** @format int32 */
  statusCode: number;
  successes: IReadOnlyListOfSuccessDTO;
  value?: RefreshPlexAccountAccessRapportDTO[] | null;
}

export interface ResultDTOOfListOfServerDownloadProgressDTO {
  errors: IReadOnlyListOfErrorDTO;
  isSuccess: boolean;
  /** @format int32 */
  statusCode: number;
  successes: IReadOnlyListOfSuccessDTO;
  value?: ServerDownloadProgressDTO[] | null;
}

export interface ResultDTOOfListOfString {
  errors: IReadOnlyListOfErrorDTO;
  isSuccess: boolean;
  /** @format int32 */
  statusCode: number;
  successes: IReadOnlyListOfSuccessDTO;
  value?: string[] | null;
}

export interface ResultDTOOfPhotoAlbumPageDTO {
  errors: IReadOnlyListOfErrorDTO;
  isSuccess: boolean;
  /** @format int32 */
  statusCode: number;
  successes: IReadOnlyListOfSuccessDTO;
  value?: PhotoAlbumPageDTO | null;
}

export interface ResultDTOOfPhotoAssetInspectionDTO {
  errors: IReadOnlyListOfErrorDTO;
  isSuccess: boolean;
  /** @format int32 */
  statusCode: number;
  successes: IReadOnlyListOfSuccessDTO;
  value?: PhotoAssetInspectionDTO | null;
}

export interface ResultDTOOfPhotoAssetPageDTO {
  errors: IReadOnlyListOfErrorDTO;
  isSuccess: boolean;
  /** @format int32 */
  statusCode: number;
  successes: IReadOnlyListOfSuccessDTO;
  value?: PhotoAssetPageDTO | null;
}

export interface ResultDTOOfPhotoFolderChildrenDTO {
  errors: IReadOnlyListOfErrorDTO;
  isSuccess: boolean;
  /** @format int32 */
  statusCode: number;
  successes: IReadOnlyListOfSuccessDTO;
  value?: PhotoFolderChildrenDTO | null;
}

export interface ResultDTOOfPhotoFolderPageDTO {
  errors: IReadOnlyListOfErrorDTO;
  isSuccess: boolean;
  /** @format int32 */
  statusCode: number;
  successes: IReadOnlyListOfSuccessDTO;
  value?: PhotoFolderPageDTO | null;
}

export interface ResultDTOOfPlexAccountDTO {
  errors: IReadOnlyListOfErrorDTO;
  isSuccess: boolean;
  /** @format int32 */
  statusCode: number;
  successes: IReadOnlyListOfSuccessDTO;
  value?: PlexAccountDTO | null;
}

export interface ResultDTOOfPlexLibraryAccessTimelineDTO {
  errors: IReadOnlyListOfErrorDTO;
  isSuccess: boolean;
  /** @format int32 */
  statusCode: number;
  successes: IReadOnlyListOfSuccessDTO;
  value?: PlexLibraryAccessTimelineDTO | null;
}

export interface ResultDTOOfPlexLibraryDTO {
  errors: IReadOnlyListOfErrorDTO;
  isSuccess: boolean;
  /** @format int32 */
  statusCode: number;
  successes: IReadOnlyListOfSuccessDTO;
  value?: PlexLibraryDTO | null;
}

export interface ResultDTOOfPlexMediaComparisonDetailsDTO {
  errors: IReadOnlyListOfErrorDTO;
  isSuccess: boolean;
  /** @format int32 */
  statusCode: number;
  successes: IReadOnlyListOfSuccessDTO;
  value?: PlexMediaComparisonDetailsDTO | null;
}

export interface ResultDTOOfPlexMediaDTO {
  errors: IReadOnlyListOfErrorDTO;
  isSuccess: boolean;
  /** @format int32 */
  statusCode: number;
  successes: IReadOnlyListOfSuccessDTO;
  value?: PlexMediaDTO | null;
}

export interface ResultDTOOfPlexMediaFilterMetadataDTO {
  errors: IReadOnlyListOfErrorDTO;
  isSuccess: boolean;
  /** @format int32 */
  statusCode: number;
  successes: IReadOnlyListOfSuccessDTO;
  value?: PlexMediaFilterMetadataDTO | null;
}

export interface ResultDTOOfPlexMediaMetadataDTO {
  errors: IReadOnlyListOfErrorDTO;
  isSuccess: boolean;
  /** @format int32 */
  statusCode: number;
  successes: IReadOnlyListOfSuccessDTO;
  value?: PlexMediaMetadataDTO | null;
}

export interface ResultDTOOfPlexMediaStatisticsDTO {
  errors: IReadOnlyListOfErrorDTO;
  isSuccess: boolean;
  /** @format int32 */
  statusCode: number;
  successes: IReadOnlyListOfSuccessDTO;
  value?: PlexMediaStatisticsDTO | null;
}

export interface ResultDTOOfPlexServerConnectionDTO {
  errors: IReadOnlyListOfErrorDTO;
  isSuccess: boolean;
  /** @format int32 */
  statusCode: number;
  successes: IReadOnlyListOfSuccessDTO;
  value?: PlexServerConnectionDTO | null;
}

export interface ResultDTOOfPlexServerDTO {
  errors: IReadOnlyListOfErrorDTO;
  isSuccess: boolean;
  /** @format int32 */
  statusCode: number;
  successes: IReadOnlyListOfSuccessDTO;
  value?: PlexServerDTO | null;
}

export interface ResultDTOOfPlexServerStatusDTO {
  errors: IReadOnlyListOfErrorDTO;
  isSuccess: boolean;
  /** @format int32 */
  statusCode: number;
  successes: IReadOnlyListOfSuccessDTO;
  value?: PlexServerStatusDTO | null;
}

export interface ResultDTOOfRadarrIntegrationDTO {
  errors: IReadOnlyListOfErrorDTO;
  isSuccess: boolean;
  /** @format int32 */
  statusCode: number;
  successes: IReadOnlyListOfSuccessDTO;
  value?: RadarrIntegrationDTO | null;
}

export interface ResultDTOOfServerIdentityDTO {
  errors: IReadOnlyListOfErrorDTO;
  isSuccess: boolean;
  /** @format int32 */
  statusCode: number;
  successes: IReadOnlyListOfSuccessDTO;
  value?: ServerIdentityDTO | null;
}

export interface ResultDTOOfSettingsModelDTO {
  errors: IReadOnlyListOfErrorDTO;
  isSuccess: boolean;
  /** @format int32 */
  statusCode: number;
  successes: IReadOnlyListOfSuccessDTO;
  value?: SettingsModelDTO | null;
}

export interface ResultDTOOfSonarrIntegrationDTO {
  errors: IReadOnlyListOfErrorDTO;
  isSuccess: boolean;
  /** @format int32 */
  statusCode: number;
  successes: IReadOnlyListOfSuccessDTO;
  value?: SonarrIntegrationDTO | null;
}

export interface ResultDTOOfString {
  errors: IReadOnlyListOfErrorDTO;
  isSuccess: boolean;
  /** @format int32 */
  statusCode: number;
  successes: IReadOnlyListOfSuccessDTO;
  value?: string | null;
}

export interface ResultDTOOfTestConnectionToRadarrEndpointResponse {
  errors: IReadOnlyListOfErrorDTO;
  isSuccess: boolean;
  /** @format int32 */
  statusCode: number;
  successes: IReadOnlyListOfSuccessDTO;
  value?: TestConnectionToRadarrEndpointResponse | null;
}

export interface ResultDTOOfTestConnectionToSonarrEndpointResponse {
  errors: IReadOnlyListOfErrorDTO;
  isSuccess: boolean;
  /** @format int32 */
  statusCode: number;
  successes: IReadOnlyListOfSuccessDTO;
  value?: TestConnectionToSonarrEndpointResponse | null;
}

export interface ResultDTOOfUserClaimsDTO {
  errors: IReadOnlyListOfErrorDTO;
  isSuccess: boolean;
  /** @format int32 */
  statusCode: number;
  successes: IReadOnlyListOfSuccessDTO;
  value?: UserClaimsDTO | null;
}

export interface ResultDTOOfValidatePlexCredentialsDTO {
  errors: IReadOnlyListOfErrorDTO;
  isSuccess: boolean;
  /** @format int32 */
  statusCode: number;
  successes: IReadOnlyListOfSuccessDTO;
  value?: ValidatePlexCredentialsDTO | null;
}

export interface ResultDTOOfValidatePlexTokenEndpointResponse {
  errors: IReadOnlyListOfErrorDTO;
  isSuccess: boolean;
  /** @format int32 */
  statusCode: number;
  successes: IReadOnlyListOfSuccessDTO;
  value?: ValidatePlexTokenEndpointResponse | null;
}

export interface ServerConnectionCheckStatusProgressDTO {
  completed: boolean;
  connectionSuccessful: boolean;
  message: string;
  /** @format int32 */
  plexServerConnectionId: number;
  /** @format int32 */
  plexServerId: number;
  /** @format int32 */
  retryAttemptCount: number;
  /** @format int32 */
  retryAttemptIndex: number;
  /** @format int32 */
  statusCode: number;
  /** @format int32 */
  timeToNextRetry: number;
}

export interface ServerDownloadProgressDTO {
  /** @format int32 */
  downloadableTasksCount: number;
  downloads: ListOfDownloadProgressDTO;
  /** @format int32 */
  id: number;
}

export interface ServerDownloadProgressMessagePackDTO {
  /** @format int32 */
  downloadableTasksCount: number;
  downloads: ListOfDownloadProgressMessagePackDTO;
  /** @format int32 */
  id: number;
}

export interface ServerIdentityDTO {
  claimed: boolean;
  machineIdentifier: string;
  version: string;
}

export interface ServerSettingsDTO {
  data: ListOfPlexServerSettingItemModule;
}

export interface SetLibraryEnabledRequest {
  isEnabled: boolean;
}

export interface SetNotificationVisibilityEndpointRequest {
  hidden: boolean;
  /**
   * @format int32
   * @min 0
   * @exclusiveMin true
   */
  id: number;
}

export interface SetServerAliasRequest {
  serverAlias: string;
}

export interface SetServerEnabledRequest {
  isEnabled: boolean;
}

export interface SetServerOwnedRequest {
  isOwned: boolean;
}

export interface SettingsModelDTO {
  confirmationSettings: ConfirmationSettingsDTO;
  dateTimeSettings: DateTimeSettingsDTO;
  debugSettings: DebugSettingsDTO;
  displaySettings: DisplaySettingsDTO;
  downloadManagerSettings: DownloadManagerSettingsDTO;
  generalSettings: GeneralSettingsDTO;
  languageSettings: LanguageSettingsDTO;
  networkSettings: NetworkSettingsDTO;
  serverSettings: ServerSettingsDTO;
}

export interface SonarrIntegrationDTO {
  apiKey: string;
  category: string;
  /** @format int32 */
  downloadFolderId: number;
  /** @format uuid */
  id: string;
  lastConnectionTestErrorMessage?: string | null;
  /** @format int32 */
  lastConnectionTestHttpStatusCode?: number | null;
  lastConnectionTestStatus: TestConnectionStatus;
  /** @format date-time */
  lastConnectionTestedAt?: string | null;
  name: string;
  provisioningState: IntegrationProvisioningState;
  url: string;
}

export interface Sort {
  direction: SortDirection;
  field: SortType;
}

export enum SortDirection {
  Asc = "asc",
  Desc = "desc",
  NoSort = "no-sort",
}

export enum SortType {
  Title = "title",
  Year = "year",
  AddedAt = "addedAt",
  UpdatedAt = "updatedAt",
  Duration = "duration",
  MediaSize = "mediaSize",
  VideoQuality = "videoQuality",
  AudioQuality = "audioQuality",
  ComparisonState = "comparisonState",
}

export interface SuccessDTO {
  message: string;
  metadata: DictionaryOfStringAndObject;
}

export interface SyncPlexServerMediaEndpointRequest {
  forceLibrarySync: boolean;
  forceMediaRefresh: boolean;
}

export enum TestConnectionStatus {
  Unknown = "Unknown",
  Success = "Success",
  UrlIsInvalid = "UrlIsInvalid",
  ConnectionFailed = "ConnectionFailed",
  InvalidApiKey = "InvalidApiKey",
}

export interface TestConnectionToRadarrEndpointResponse {
  errorMessage?: string | null;
  /** @format int32 */
  httpStatusCode?: number | null;
  result: TestConnectionStatus;
  /** @format date-time */
  testedAt: string;
}

export interface TestConnectionToSonarrEndpointResponse {
  errorMessage?: string | null;
  /** @format int32 */
  httpStatusCode?: number | null;
  result: TestConnectionStatus;
  /** @format date-time */
  testedAt: string;
}

/** @example {"username":"ReaparrRocks","password":"R€Aℙℙ@rr69"} */
export interface UpdateCredentialsEndpointRequest {
  /**
   * @minLength 8
   * @example "R€Aℙℙ@rr69"
   */
  password?: string | null;
  /**
   * @minLength 8
   * @example "ReaparrRocks"
   */
  username?: string | null;
}

export interface UpdatePlexServerConnectionEndpointRequest {
  /** @minLength 1 */
  address: string;
  /**
   * @format int32
   * @min 0
   * @exclusiveMin true
   */
  id: number;
  /**
   * @format int32
   * @min 0
   * @exclusiveMin true
   */
  plexServerId: number;
  /**
   * @format int32
   * @min 0
   * @exclusiveMin true
   */
  port: number;
  /** @minLength 1 */
  protocol: string;
  /** @minLength 1 */
  url: string;
}

export interface UpdateRadarrIntegrationRequest {
  /** @minLength 1 */
  apiKey: string;
  /** @minLength 1 */
  category: string;
  /**
   * @format int32
   * @min 0
   * @exclusiveMin true
   */
  downloadFolderId: number;
  /** @minLength 1 */
  name: string;
  /** @minLength 1 */
  url: string;
}

export interface UpdateSonarrIntegrationRequest {
  /** @minLength 1 */
  apiKey: string;
  /** @minLength 1 */
  category: string;
  /**
   * @format int32
   * @min 0
   * @exclusiveMin true
   */
  downloadFolderId: number;
  /** @minLength 1 */
  name: string;
  /** @minLength 1 */
  url: string;
}

export interface UserClaimsDTO {
  claims: ListOfString;
  isLoggedIn: boolean;
  userName: string;
}

export interface ValidatePlexCredentialsDTO {
  is2Fa: boolean;
  authenticationToken: string;
  clientId: string;
  email: string;
  isUnAuthorized: boolean;
  isValidated: boolean;
  password: string;
  /** @format int64 */
  plexId: number;
  title: string;
  username: string;
  uuid: string;
  /** @format date-time */
  validatedAt?: string | null;
}

export interface ValidatePlexCredentialsEndpointRequest {
  clientId: string;
  displayName: string;
  /** @minLength 5 */
  password: string;
  /** @minLength 5 */
  username: string;
  verificationCode: string;
}

export interface ValidatePlexServerConnectionEndpointRequest {
  /** @minLength 1 */
  url: string;
}

export interface ValidatePlexTokenEndpointRequest {
  displayName: string;
  /** @minLength 5 */
  manualAuthenticationToken: string;
  /**
   * @format int32
   * @min 0
   * @exclusiveMin false
   */
  plexAccountId: number;
}

export interface ValidatePlexTokenEndpointResponse {
  is2Fa: boolean;
  clientId: string;
  customAuthenticationToken: string;
  email: string;
  isUnAuthorized: boolean;
  isValidated: boolean;
  /** @format int64 */
  plexId: number;
  title: string;
  username: string;
  uuid: string;
  /** @format date-time */
  validatedAt?: string | null;
}

export enum VideoQuality {
  Unknown = "Unknown",
  SubSD144P = "SubSD_144p",
  SubSDCIF = "SubSD_CIF",
  NHD = "nHD",
  SD = "SD",
  DVD = "DVD",
  HD = "HD",
  FullHD = "FullHD",
  QHD = "QHD",
  UHD_4K = "UHD_4K",
  UHD_8K = "UHD_8K",
  None = "None",
}

export enum ViewMode {
  Poster = "Poster",
  Table = "Table",
}
