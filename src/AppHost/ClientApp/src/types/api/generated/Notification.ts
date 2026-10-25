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
  CountResponseDTO,
  NotificationDTO,
  SetNotificationVisibilityEndpointRequest,
} from "./data-contracts";

import { apiCheckPipe, axiosObservable } from "@api/base";
import queryString from "query-string";

export class Notification {
  /**
   * No description
   * * @tags Notification
   * @name GetAllNotificationsEndpoint
   * @request GET:/api/Notification
   * @secure
   */
  getAllNotificationsEndpoint = (params: RequestParams = {}) =>
    axiosObservable<NotificationDTO[]>({
      url: `/api/Notification`,
      method: "GET",
      secure: true,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<NotificationDTO[]>);

  /**
   * No description
   * * @tags Notification
   * @name SetNotificationVisibilityEndpoint
   * @request PATCH:/api/Notification
   * @secure
   */
  setNotificationVisibilityEndpoint = (
    data: SetNotificationVisibilityEndpointRequest,
    params: RequestParams = {},
  ) =>
    axiosObservable<BaseResultDTO>({
      url: `/api/Notification`,
      method: "PATCH",
      data: data,
      secure: true,
      type: ContentType.Json,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<BaseResultDTO>);

  /**
   * No description
   * * @tags Notification
   * @name ClearAllNotificationsEndpoint
   * @request DELETE:/api/Notification/clear
   * @secure
   */
  clearAllNotificationsEndpoint = (params: RequestParams = {}) =>
    axiosObservable<CountResponseDTO>({
      url: `/api/Notification/clear`,
      method: "DELETE",
      secure: true,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<CountResponseDTO>);
}

export class NotificationPaths {
  static getAllNotificationsEndpoint = () =>
    queryString.stringifyUrl({ url: `/api/Notification` });

  static setNotificationVisibilityEndpoint = () =>
    queryString.stringifyUrl({ url: `/api/Notification` });

  static clearAllNotificationsEndpoint = () =>
    queryString.stringifyUrl({ url: `/api/Notification/clear` });
}
