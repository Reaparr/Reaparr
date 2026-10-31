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
  CreateRadarrIntegrationRequest,
  CreateSonarrIntegrationRequest,
  IntegrationSummary,
  RadarrIntegrationDTO,
  SonarrIntegrationDTO,
  TestConnectionToRadarrEndpointResponse,
  TestConnectionToSonarrEndpointResponse,
  UpdateRadarrIntegrationRequest,
  UpdateSonarrIntegrationRequest,
} from "./data-contracts";

import { apiCheckPipe, axiosObservable } from "@api/base";
import queryString from "query-string";

export class Integration {
  /**
   * No description
   * * @tags Integration
   * @name GetIntegrationsEndpoint
   * @request GET:/api/Integration
   * @secure
   */
  getIntegrationsEndpoint = (params: RequestParams = {}) =>
    axiosObservable<IntegrationSummary[]>({
      url: `/api/Integration`,
      method: "GET",
      secure: true,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<IntegrationSummary[]>);

  /**
   * No description
   * * @tags Integration
   * @name ClearRadarrConfigurationEndpoint
   * @request DELETE:/api/Integration/Radarr/Configuration
   * @secure
   */
  clearRadarrConfigurationEndpoint = (params: RequestParams = {}) =>
    axiosObservable<BaseResultDTO>({
      url: `/api/Integration/Radarr/Configuration`,
      method: "DELETE",
      secure: true,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<BaseResultDTO>);

  /**
   * No description
   * * @tags Integration
   * @name CreateRadarrIntegrationEndpoint
   * @request POST:/api/Integration/Radarr/Configure
   * @secure
   */
  createRadarrIntegrationEndpoint = (
    data: CreateRadarrIntegrationRequest,
    params: RequestParams = {},
  ) =>
    axiosObservable<RadarrIntegrationDTO>({
      url: `/api/Integration/Radarr/Configure`,
      method: "POST",
      data: data,
      secure: true,
      type: ContentType.Json,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<RadarrIntegrationDTO>);

  /**
   * No description
   * * @tags Integration
   * @name TestConnectionToRadarrEndpoint
   * @request GET:/api/Integration/Radarr/TestConnection
   * @secure
   */
  testConnectionToRadarrEndpoint = (
    query?: {
      apiKey?: string | null;
      /** @format uuid */
      integrationId?: string | null;
      url?: string | null;
    },
    params: RequestParams = {},
  ) =>
    axiosObservable<TestConnectionToRadarrEndpointResponse>({
      url: `/api/Integration/Radarr/TestConnection`,
      method: "GET",
      params: query,
      secure: true,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<TestConnectionToRadarrEndpointResponse>);

  /**
   * No description
   * * @tags Integration
   * @name DeleteRadarrIntegrationEndpoint
   * @request DELETE:/api/Integration/Radarr/{integrationId}
   * @secure
   */
  deleteRadarrIntegrationEndpoint = (
    integrationId: string,
    query: {
      force: boolean;
    },
    params: RequestParams = {},
  ) =>
    axiosObservable<void>({
      url: `/api/Integration/Radarr/${integrationId}`,
      method: "DELETE",
      params: query,
      secure: true,
      ...params,
    }).pipe(apiCheckPipe<void>);

  /**
   * No description
   * * @tags Integration
   * @name GetRadarrIntegrationEndpoint
   * @request GET:/api/Integration/Radarr/{integrationId}
   * @secure
   */
  getRadarrIntegrationEndpoint = (
    integrationId: string,
    params: RequestParams = {},
  ) =>
    axiosObservable<RadarrIntegrationDTO>({
      url: `/api/Integration/Radarr/${integrationId}`,
      method: "GET",
      secure: true,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<RadarrIntegrationDTO>);

  /**
   * No description
   * * @tags Integration
   * @name UpdateRadarrIntegrationEndpoint
   * @request PUT:/api/Integration/Radarr/{integrationId}/Configure
   * @secure
   */
  updateRadarrIntegrationEndpoint = (
    integrationId: string,
    data: UpdateRadarrIntegrationRequest,
    params: RequestParams = {},
  ) =>
    axiosObservable<RadarrIntegrationDTO>({
      url: `/api/Integration/Radarr/${integrationId}/Configure`,
      method: "PUT",
      data: data,
      secure: true,
      type: ContentType.Json,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<RadarrIntegrationDTO>);

  /**
   * No description
   * * @tags Integration
   * @name SetupRadarrIntegrationEndpoint
   * @request POST:/api/Integration/Radarr/{integrationId}/Setup
   * @secure
   */
  setupRadarrIntegrationEndpoint = (
    integrationId: string,
    params: RequestParams = {},
  ) =>
    axiosObservable<RadarrIntegrationDTO>({
      url: `/api/Integration/Radarr/${integrationId}/Setup`,
      method: "POST",
      secure: true,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<RadarrIntegrationDTO>);

  /**
   * No description
   * * @tags Integration
   * @name ClearSonarrConfigurationEndpoint
   * @request DELETE:/api/Integration/Sonarr/Configuration
   * @secure
   */
  clearSonarrConfigurationEndpoint = (params: RequestParams = {}) =>
    axiosObservable<BaseResultDTO>({
      url: `/api/Integration/Sonarr/Configuration`,
      method: "DELETE",
      secure: true,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<BaseResultDTO>);

  /**
   * No description
   * * @tags Integration
   * @name CreateSonarrIntegrationEndpoint
   * @request POST:/api/Integration/Sonarr/Configure
   * @secure
   */
  createSonarrIntegrationEndpoint = (
    data: CreateSonarrIntegrationRequest,
    params: RequestParams = {},
  ) =>
    axiosObservable<SonarrIntegrationDTO>({
      url: `/api/Integration/Sonarr/Configure`,
      method: "POST",
      data: data,
      secure: true,
      type: ContentType.Json,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<SonarrIntegrationDTO>);

  /**
   * No description
   * * @tags Integration
   * @name TestConnectionToSonarrEndpoint
   * @request GET:/api/Integration/Sonarr/TestConnection
   * @secure
   */
  testConnectionToSonarrEndpoint = (
    query?: {
      apiKey?: string | null;
      /** @format uuid */
      integrationId?: string | null;
      url?: string | null;
    },
    params: RequestParams = {},
  ) =>
    axiosObservable<TestConnectionToSonarrEndpointResponse>({
      url: `/api/Integration/Sonarr/TestConnection`,
      method: "GET",
      params: query,
      secure: true,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<TestConnectionToSonarrEndpointResponse>);

  /**
   * No description
   * * @tags Integration
   * @name DeleteSonarrIntegrationEndpoint
   * @request DELETE:/api/Integration/Sonarr/{integrationId}
   * @secure
   */
  deleteSonarrIntegrationEndpoint = (
    integrationId: string,
    query: {
      force: boolean;
    },
    params: RequestParams = {},
  ) =>
    axiosObservable<void>({
      url: `/api/Integration/Sonarr/${integrationId}`,
      method: "DELETE",
      params: query,
      secure: true,
      ...params,
    }).pipe(apiCheckPipe<void>);

  /**
   * No description
   * * @tags Integration
   * @name GetSonarrIntegrationEndpoint
   * @request GET:/api/Integration/Sonarr/{integrationId}
   * @secure
   */
  getSonarrIntegrationEndpoint = (
    integrationId: string,
    params: RequestParams = {},
  ) =>
    axiosObservable<SonarrIntegrationDTO>({
      url: `/api/Integration/Sonarr/${integrationId}`,
      method: "GET",
      secure: true,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<SonarrIntegrationDTO>);

  /**
   * No description
   * * @tags Integration
   * @name UpdateSonarrIntegrationEndpoint
   * @request PUT:/api/Integration/Sonarr/{integrationId}/Configure
   * @secure
   */
  updateSonarrIntegrationEndpoint = (
    integrationId: string,
    data: UpdateSonarrIntegrationRequest,
    params: RequestParams = {},
  ) =>
    axiosObservable<SonarrIntegrationDTO>({
      url: `/api/Integration/Sonarr/${integrationId}/Configure`,
      method: "PUT",
      data: data,
      secure: true,
      type: ContentType.Json,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<SonarrIntegrationDTO>);

  /**
   * No description
   * * @tags Integration
   * @name SetupSonarrIntegrationEndpoint
   * @request POST:/api/Integration/Sonarr/{integrationId}/Setup
   * @secure
   */
  setupSonarrIntegrationEndpoint = (
    integrationId: string,
    params: RequestParams = {},
  ) =>
    axiosObservable<SonarrIntegrationDTO>({
      url: `/api/Integration/Sonarr/${integrationId}/Setup`,
      method: "POST",
      secure: true,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<SonarrIntegrationDTO>);
}

export class IntegrationPaths {
  static getIntegrationsEndpoint = () =>
    queryString.stringifyUrl({ url: `/api/Integration` });

  static clearRadarrConfigurationEndpoint = () =>
    queryString.stringifyUrl({ url: `/api/Integration/Radarr/Configuration` });

  static createRadarrIntegrationEndpoint = () =>
    queryString.stringifyUrl({ url: `/api/Integration/Radarr/Configure` });

  static testConnectionToRadarrEndpoint = (query?: {
    apiKey?: string | null;
    /** @format uuid */
    integrationId?: string | null;
    url?: string | null;
  }) =>
    queryString.stringifyUrl({
      url: `/api/Integration/Radarr/TestConnection`,
      query,
    });

  static deleteRadarrIntegrationEndpoint = (
    integrationId: string,
    query: {
      force: boolean;
    },
  ) =>
    queryString.stringifyUrl({
      url: `/api/Integration/Radarr/${integrationId}`,
      query,
    });

  static getRadarrIntegrationEndpoint = (integrationId: string) =>
    queryString.stringifyUrl({
      url: `/api/Integration/Radarr/${integrationId}`,
    });

  static updateRadarrIntegrationEndpoint = (integrationId: string) =>
    queryString.stringifyUrl({
      url: `/api/Integration/Radarr/${integrationId}/Configure`,
    });

  static setupRadarrIntegrationEndpoint = (integrationId: string) =>
    queryString.stringifyUrl({
      url: `/api/Integration/Radarr/${integrationId}/Setup`,
    });

  static clearSonarrConfigurationEndpoint = () =>
    queryString.stringifyUrl({ url: `/api/Integration/Sonarr/Configuration` });

  static createSonarrIntegrationEndpoint = () =>
    queryString.stringifyUrl({ url: `/api/Integration/Sonarr/Configure` });

  static testConnectionToSonarrEndpoint = (query?: {
    apiKey?: string | null;
    /** @format uuid */
    integrationId?: string | null;
    url?: string | null;
  }) =>
    queryString.stringifyUrl({
      url: `/api/Integration/Sonarr/TestConnection`,
      query,
    });

  static deleteSonarrIntegrationEndpoint = (
    integrationId: string,
    query: {
      force: boolean;
    },
  ) =>
    queryString.stringifyUrl({
      url: `/api/Integration/Sonarr/${integrationId}`,
      query,
    });

  static getSonarrIntegrationEndpoint = (integrationId: string) =>
    queryString.stringifyUrl({
      url: `/api/Integration/Sonarr/${integrationId}`,
    });

  static updateSonarrIntegrationEndpoint = (integrationId: string) =>
    queryString.stringifyUrl({
      url: `/api/Integration/Sonarr/${integrationId}/Configure`,
    });

  static setupSonarrIntegrationEndpoint = (integrationId: string) =>
    queryString.stringifyUrl({
      url: `/api/Integration/Sonarr/${integrationId}/Setup`,
    });
}
