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
  CreatePlexServerConnectionEndpointRequest,
  PlexServerConnectionDTO,
  PlexServerStatusDTO,
  ServerIdentityDTO,
  UpdatePlexServerConnectionEndpointRequest,
  ValidatePlexServerConnectionEndpointRequest,
} from "./data-contracts";

import { apiCheckPipe, axiosObservable } from "@api/base";
import queryString from "query-string";

export class PlexServerConnection {
  /**
   * No description
   * * @tags Plexserverconnection
   * @name CreatePlexServerConnectionEndpoint
   * @request POST:/api/PlexServerConnection
   * @secure
   */
  createPlexServerConnectionEndpoint = (
    data: CreatePlexServerConnectionEndpointRequest,
    params: RequestParams = {},
  ) =>
    axiosObservable<PlexServerConnectionDTO>({
      url: `/api/PlexServerConnection`,
      method: "POST",
      data: data,
      secure: true,
      type: ContentType.Json,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<PlexServerConnectionDTO>);

  /**
   * No description
   * * @tags Plexserverconnection
   * @name GetAllPlexServerConnectionsEndpoint
   * @request GET:/api/PlexServerConnection
   * @secure
   */
  getAllPlexServerConnectionsEndpoint = (params: RequestParams = {}) =>
    axiosObservable<PlexServerConnectionDTO[]>({
      url: `/api/PlexServerConnection`,
      method: "GET",
      secure: true,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<PlexServerConnectionDTO[]>);

  /**
   * No description
   * * @tags Plexserverconnection
   * @name UpdatePlexServerConnectionEndpoint
   * @request PATCH:/api/PlexServerConnection
   * @secure
   */
  updatePlexServerConnectionEndpoint = (
    data: UpdatePlexServerConnectionEndpointRequest,
    params: RequestParams = {},
  ) =>
    axiosObservable<PlexServerConnectionDTO>({
      url: `/api/PlexServerConnection`,
      method: "PATCH",
      data: data,
      secure: true,
      type: ContentType.Json,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<PlexServerConnectionDTO>);

  /**
   * No description
   * * @tags Plexserverconnection
   * @name CheckAllConnectionsStatusByPlexServerEndpoint
   * @request GET:/api/PlexServerConnection/check/by-server/{plexServerId}
   * @secure
   */
  checkAllConnectionsStatusByPlexServerEndpoint = (
    plexServerId: number,
    params: RequestParams = {},
  ) =>
    axiosObservable<PlexServerStatusDTO[]>({
      url: `/api/PlexServerConnection/check/by-server/${plexServerId}`,
      method: "GET",
      secure: true,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<PlexServerStatusDTO[]>);

  /**
   * No description
   * * @tags Plexserverconnection
   * @name CheckConnectionStatusByIdEndpoint
   * @request GET:/api/PlexServerConnection/check/{plexServerConnectionId}
   * @secure
   */
  checkConnectionStatusByIdEndpoint = (
    plexServerConnectionId: number,
    params: RequestParams = {},
  ) =>
    axiosObservable<PlexServerStatusDTO>({
      url: `/api/PlexServerConnection/check/${plexServerConnectionId}`,
      method: "GET",
      secure: true,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<PlexServerStatusDTO>);

  /**
   * No description
   * * @tags Plexserverconnection
   * @name ValidatePlexServerConnectionEndpoint
   * @request POST:/api/PlexServerConnection/validate
   * @secure
   */
  validatePlexServerConnectionEndpoint = (
    data: ValidatePlexServerConnectionEndpointRequest,
    params: RequestParams = {},
  ) =>
    axiosObservable<ServerIdentityDTO>({
      url: `/api/PlexServerConnection/validate`,
      method: "POST",
      data: data,
      secure: true,
      type: ContentType.Json,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<ServerIdentityDTO>);

  /**
   * No description
   * * @tags Plexserverconnection
   * @name DeletePlexServerConnectionById
   * @request DELETE:/api/PlexServerConnection/{plexServerConnectionId}
   * @secure
   */
  deletePlexServerConnectionById = (
    plexServerConnectionId: number,
    params: RequestParams = {},
  ) =>
    axiosObservable<BaseResultDTO>({
      url: `/api/PlexServerConnection/${plexServerConnectionId}`,
      method: "DELETE",
      secure: true,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<BaseResultDTO>);

  /**
   * No description
   * * @tags Plexserverconnection
   * @name GetPlexServerConnectionByIdEndpoint
   * @request GET:/api/PlexServerConnection/{plexServerConnectionId}
   * @secure
   */
  getPlexServerConnectionByIdEndpoint = (
    plexServerConnectionId: number,
    params: RequestParams = {},
  ) =>
    axiosObservable<PlexServerConnectionDTO>({
      url: `/api/PlexServerConnection/${plexServerConnectionId}`,
      method: "GET",
      secure: true,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<PlexServerConnectionDTO>);
}

export class PlexServerConnectionPaths {
  static createPlexServerConnectionEndpoint = () =>
    queryString.stringifyUrl({ url: `/api/PlexServerConnection` });

  static getAllPlexServerConnectionsEndpoint = () =>
    queryString.stringifyUrl({ url: `/api/PlexServerConnection` });

  static updatePlexServerConnectionEndpoint = () =>
    queryString.stringifyUrl({ url: `/api/PlexServerConnection` });

  static checkAllConnectionsStatusByPlexServerEndpoint = (
    plexServerId: number,
  ) =>
    queryString.stringifyUrl({
      url: `/api/PlexServerConnection/check/by-server/${plexServerId}`,
    });

  static checkConnectionStatusByIdEndpoint = (plexServerConnectionId: number) =>
    queryString.stringifyUrl({
      url: `/api/PlexServerConnection/check/${plexServerConnectionId}`,
    });

  static validatePlexServerConnectionEndpoint = () =>
    queryString.stringifyUrl({ url: `/api/PlexServerConnection/validate` });

  static deletePlexServerConnectionById = (plexServerConnectionId: number) =>
    queryString.stringifyUrl({
      url: `/api/PlexServerConnection/${plexServerConnectionId}`,
    });

  static getPlexServerConnectionByIdEndpoint = (
    plexServerConnectionId: number,
  ) =>
    queryString.stringifyUrl({
      url: `/api/PlexServerConnection/${plexServerConnectionId}`,
    });
}
