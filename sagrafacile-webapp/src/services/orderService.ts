import apiClient from './apiClient';
import { PaginatedResult, OrderDto, OrderQueryParameters } from '@/types';

const getOrders = async (params: OrderQueryParameters): Promise<PaginatedResult<OrderDto>> => {
    const response = await apiClient.get<PaginatedResult<OrderDto>>('/orders', { params });
    return response.data;
};

const orderService = {
    getOrders,
};

export default orderService;
