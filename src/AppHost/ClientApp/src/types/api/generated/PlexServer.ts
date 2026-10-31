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

import type { RequestParams } from "./http-client";
import { ContentType } from "./http-client";

import type {
  BaseResultDTO,
  PlexServerDTO,
  RefreshPlexAccountAccessRapportDTO,
  SetServerAliasRequest,
  SetServerEnabledRequest,
  SetServerOwnedRequest,
  SyncPlexServerMediaEndpointRequest,
} from "./data-contracts";

import { apiCheckPipe, axiosObservable } from "@api/base";
import queryString from "query-string";

export class PlexServer {
  /**
   * @description  Retrieves all the PlexServers, without PlexLibraries but with all its connections currently in the database.
   * * @tags Plexserver
   * @name GetAllPlexServersEndpoint
   * @summary Get All the PlexServers, without PlexLibraries but with all its connections.
   * @request GET:/api/PlexServer
   * @secure
   */
  getAllPlexServersEndpoint = (params: RequestParams = {}) =>
    axiosObservable<PlexServerDTO[]>({
      url: `/api/PlexServer`,
      method: "GET",
      secure: true,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<PlexServerDTO[]>);

  /**
   * No description
   * * @tags Plexserver
   * @name PausePlexServerDownloadsEndpoint
   * @request PUT:/api/PlexServer/server/pause/{plexServerId}
   * @secure
   */
  pausePlexServerDownloadsEndpoint = (
    plexServerId: number,
    params: RequestParams = {},
  ) =>
    axiosObservable<BaseResultDTO>({
      url: `/api/PlexServer/server/pause/${plexServerId}`,
      method: "PUT",
      secure: true,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<BaseResultDTO>);

  /**
   * No description
   * * @tags Plexserver
   * @name ResumePlexServerDownloadsEndpoint
   * @request PUT:/api/PlexServer/server/resume/{plexServerId}
   * @secure
   */
  resumePlexServerDownloadsEndpoint = (
    plexServerId: number,
    params: RequestParams = {},
  ) =>
    axiosObservable<BaseResultDTO>({
      url: `/api/PlexServer/server/resume/${plexServerId}`,
      method: "PUT",
      secure: true,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<BaseResultDTO>);

  /**
   * No description
   * * @tags Plexserver
   * @name DeletePlexServerEndpoint
   * @request DELETE:/api/PlexServer/{plexServerId}
   * @secure
   */
  deletePlexServerEndpoint = (
    plexServerId: number,
    params: RequestParams = {},
  ) =>
    axiosObservable<BaseResultDTO>({
      url: `/api/PlexServer/${plexServerId}`,
      method: "DELETE",
      secure: true,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<BaseResultDTO>);

  /**
   * No description
   * * @tags Plexserver
   * @name GetPlexServerByIdEndpoint
   * @request GET:/api/PlexServer/{plexServerId}
   * @secure
   */
  getPlexServerByIdEndpoint = (
    plexServerId: number,
    params: RequestParams = {},
  ) =>
    axiosObservable<PlexServerDTO>({
      url: `/api/PlexServer/${plexServerId}`,
      method: "GET",
      secure: true,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<PlexServerDTO>);

  /**
   * No description
   * * @tags Plexserver
   * @name QueueInspectPlexServerJobEndpoint
   * @request GET:/api/PlexServer/{plexServerId}/inspect
   * @secure
   */
  queueInspectPlexServerJobEndpoint = (
    plexServerId: number,
    params: RequestParams = {},
  ) =>
    axiosObservable<BaseResultDTO>({
      url: `/api/PlexServer/${plexServerId}/inspect`,
      method: "GET",
      secure: true,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<BaseResultDTO>);

  /**
   * No description
   * * @tags Plexserver
   * @name SetPreferredPlexServerConnectionEndpoint
   * @request GET:/api/PlexServer/{plexServerId}/preferred-connection/{plexServerConnectionId}
   * @secure
   */
  setPreferredPlexServerConnectionEndpoint = (
    plexServerId: number,
    plexServerConnectionId: number,
    params: RequestParams = {},
  ) =>
    axiosObservable<BaseResultDTO>({
      url: `/api/PlexServer/${plexServerId}/preferred-connection/${plexServerConnectionId}`,
      method: "GET",
      secure: true,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<BaseResultDTO>);

  /**
   * No description
   * * @tags Plexserver
   * @name RefreshPlexServerConnectionsEndpoint
   * @request GET:/api/PlexServer/{plexServerId}/refresh
   * @secure
   */
  refreshPlexServerConnectionsEndpoint = (
    plexServerId: number,
    params: RequestParams = {},
  ) =>
    axiosObservable<PlexServerDTO>({
      url: `/api/PlexServer/${plexServerId}/refresh`,
      method: "GET",
      secure: true,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<PlexServerDTO>);

  /**
   * No description
   * * @tags Plexserver
   * @name RefreshPlexServerAccountsAccessEndpoint
   * @request POST:/api/PlexServer/{plexServerId}/refresh-access
   * @secure
   */
  refreshPlexServerAccountsAccessEndpoint = (
    plexServerId: number,
    params: RequestParams = {},
  ) =>
    axiosObservable<RefreshPlexAccountAccessRapportDTO[]>({
      url: `/api/PlexServer/${plexServerId}/refresh-access`,
      method: "POST",
      secure: true,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<RefreshPlexAccountAccessRapportDTO[]>);

  /**
   * No description
   * * @tags Plexserver
   * @name SetServerAlias
   * @request PUT:/api/PlexServer/{plexServerId}/set-server-alias
   * @secure
   */
  setServerAlias = (
    plexServerId: number,
    data: SetServerAliasRequest,
    params: RequestParams = {},
  ) =>
    axiosObservable<BaseResultDTO>({
      url: `/api/PlexServer/${plexServerId}/set-server-alias`,
      method: "PUT",
      data: data,
      secure: true,
      type: ContentType.Json,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<BaseResultDTO>);

  /**
   * No description
   * * @tags Plexserver
   * @name SetServerEnabledEndpoint
   * @request PUT:/api/PlexServer/{plexServerId}/set-server-enabled
   * @secure
   */
  setServerEnabledEndpoint = (
    plexServerId: number,
    data: SetServerEnabledRequest,
    params: RequestParams = {},
  ) =>
    axiosObservable<PlexServerDTO>({
      url: `/api/PlexServer/${plexServerId}/set-server-enabled`,
      method: "PUT",
      data: data,
      secure: true,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<PlexServerDTO>);

  /**
   * No description
   * * @tags Plexserver
   * @name SetServerOwnedEndpoint
   * @request PUT:/api/PlexServer/{plexServerId}/set-server-owned
   * @secure
   */
  setServerOwnedEndpoint = (
    plexServerId: number,
    data: SetServerOwnedRequest,
    params: RequestParams = {},
  ) =>
    axiosObservable<PlexServerDTO>({
      url: `/api/PlexServer/${plexServerId}/set-server-owned`,
      method: "PUT",
      data: data,
      secure: true,
      type: ContentType.Json,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<PlexServerDTO>);

  /**
   * No description
   * * @tags Plexserver
   * @name SyncPlexServerMediaEndpoint
   * @request POST:/api/PlexServer/{plexServerId}/sync
   * @secure
   */
  syncPlexServerMediaEndpoint = (
    plexServerId: number,
    data: SyncPlexServerMediaEndpointRequest,
    params: RequestParams = {},
  ) =>
    axiosObservable<BaseResultDTO>({
      url: `/api/PlexServer/${plexServerId}/sync`,
      method: "POST",
      data: data,
      secure: true,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<BaseResultDTO>);
}

export class PlexServerPaths {
  static getAllPlexServersEndpoint = () =>
    queryString.stringifyUrl({ url: `/api/PlexServer` });

  static pausePlexServerDownloadsEndpoint = (plexServerId: number) =>
    queryString.stringifyUrl({
      url: `/api/PlexServer/server/pause/${plexServerId}`,
    });

  static resumePlexServerDownloadsEndpoint = (plexServerId: number) =>
    queryString.stringifyUrl({
      url: `/api/PlexServer/server/resume/${plexServerId}`,
    });

  static deletePlexServerEndpoint = (plexServerId: number) =>
    queryString.stringifyUrl({ url: `/api/PlexServer/${plexServerId}` });

  static getPlexServerByIdEndpoint = (plexServerId: number) =>
    queryString.stringifyUrl({ url: `/api/PlexServer/${plexServerId}` });

  static queueInspectPlexServerJobEndpoint = (plexServerId: number) =>
    queryString.stringifyUrl({
      url: `/api/PlexServer/${plexServerId}/inspect`,
    });

  static setPreferredPlexServerConnectionEndpoint = (
    plexServerId: number,
    plexServerConnectionId: number,
  ) =>
    queryString.stringifyUrl({
      url: `/api/PlexServer/${plexServerId}/preferred-connection/${plexServerConnectionId}`,
    });

  static refreshPlexServerConnectionsEndpoint = (plexServerId: number) =>
    queryString.stringifyUrl({
      url: `/api/PlexServer/${plexServerId}/refresh`,
    });

  static refreshPlexServerAccountsAccessEndpoint = (plexServerId: number) =>
    queryString.stringifyUrl({
      url: `/api/PlexServer/${plexServerId}/refresh-access`,
    });

  static setServerAlias = (plexServerId: number) =>
    queryString.stringifyUrl({
      url: `/api/PlexServer/${plexServerId}/set-server-alias`,
    });

  static setServerEnabledEndpoint = (plexServerId: number) =>
    queryString.stringifyUrl({
      url: `/api/PlexServer/${plexServerId}/set-server-enabled`,
    });

  static setServerOwnedEndpoint = (plexServerId: number) =>
    queryString.stringifyUrl({
      url: `/api/PlexServer/${plexServerId}/set-server-owned`,
    });

  static syncPlexServerMediaEndpoint = (plexServerId: number) =>
    queryString.stringifyUrl({ url: `/api/PlexServer/${plexServerId}/sync` });
}
