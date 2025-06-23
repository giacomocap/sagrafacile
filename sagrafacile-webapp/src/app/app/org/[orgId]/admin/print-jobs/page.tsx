'use client';

import React, { useState, useEffect, useCallback, useMemo } from 'react';
import { PrintJobDto, PaginatedResult, PrintJobQueryParameters, PrintJobStatus, PrintJobType } from '@/types';
import printerService from '@/services/printerService';
import { useAuth } from '@/contexts/AuthContext';
import { Card, CardHeader, CardTitle, CardContent, CardDescription } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from "@/components/ui/dropdown-menu";
import { MoreHorizontal, RefreshCw, AlertCircle, CheckCircle, Clock, Hourglass } from 'lucide-react';
import { toast } from 'sonner';
import PaginatedTable from '@/components/common/PaginatedTable';

const DEBOUNCE_DELAY = 300;

export default function PrintJobsPage() {
  const { user } = useAuth();
  const [printJobs, setPrintJobs] = useState<PaginatedResult<PrintJobDto> | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [isRetrying, setIsRetrying] = useState<string | null>(null); // Store job ID being retried

  const [queryParams, setQueryParams] = useState<PrintJobQueryParameters>({
    page: 1,
    pageSize: 20,
    sortBy: 'createdAt',
    sortAscending: false,
  });

  const fetchPrintJobs = useCallback(async (params: PrintJobQueryParameters) => {
    setIsLoading(true);
    setError(null);
    try {
      const response = await printerService.getPrintJobs(params);
      setPrintJobs(response);
    } catch (err) {
      console.error('Errore nel recupero dei processi di stampa:', err);
      setError('Caricamento dei processi di stampa fallito.');
      toast.error("Errore", { description: 'Caricamento dei processi di stampa fallito.' });
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    if (user) {
      const handler = setTimeout(() => {
        fetchPrintJobs(queryParams);
      }, DEBOUNCE_DELAY);
      return () => clearTimeout(handler);
    }
  }, [user, queryParams, fetchPrintJobs]);

  const handleRetryJob = async (jobId: string) => {
    setIsRetrying(jobId);
    try {
      await printerService.retryPrintJob(jobId);
      toast.success("Processo di Stampa Inviato", { description: "Il processo è stato messo in coda per un nuovo tentativo." });
      // Refresh data after a short delay to allow the backend to process
      setTimeout(() => fetchPrintJobs(queryParams), 1000);
    } catch (err: any) {
      console.error('Errore nel nuovo tentativo del processo di stampa:', err);
      const errorMessage = err.response?.data?.message || 'Nuovo tentativo fallito.';
      toast.error("Errore", { description: errorMessage });
    } finally {
      setIsRetrying(null);
    }
  };

  const handleQueryChange = (newParams: Partial<PrintJobQueryParameters>) => {
    setQueryParams(prev => ({ ...prev, ...newParams }));
  };

  const renderStatusBadge = (status: PrintJobStatus) => {
    switch (status) {
      case PrintJobStatus.Pending:
        return <Badge variant="outline"><Clock className="mr-1 h-3 w-3" /> In Coda</Badge>;
      case PrintJobStatus.Processing:
        return <Badge variant="default" className="bg-blue-500"><Hourglass className="mr-1 h-3 w-3 animate-spin" /> In Elaborazione</Badge>;
      case PrintJobStatus.Succeeded:
        return <Badge variant="secondary" className="bg-green-100 text-green-800"><CheckCircle className="mr-1 h-3 w-3" /> Riuscito</Badge>;
      case PrintJobStatus.Failed:
        return <Badge variant="destructive"><AlertCircle className="mr-1 h-3 w-3" /> Fallito</Badge>;
      default:
        return <Badge variant="outline">Sconosciuto</Badge>;
    }
  };

  const renderJobType = (type: PrintJobType) => {
    switch (type) {
      case PrintJobType.Receipt: return 'Scontrino';
      case PrintJobType.Comanda: return 'Comanda';
      case PrintJobType.TestPrint: return 'Stampa di Test';
      default: return 'Sconosciuto';
    }
  };

  const formatDate = (dateString: string | null | undefined) => {
    if (!dateString) return 'N/A';
    try {
      const date = new Date(dateString);
      // Pad with leading zeros
      const day = String(date.getDate()).padStart(2, '0');
      const month = String(date.getMonth() + 1).padStart(2, '0');
      const year = String(date.getFullYear()).slice(-2);
      const hours = String(date.getHours()).padStart(2, '0');
      const minutes = String(date.getMinutes()).padStart(2, '0');
      const seconds = String(date.getSeconds()).padStart(2, '0');
      return `${day}/${month}/${year} ${hours}:${minutes}:${seconds}`;
    } catch {
      return 'Data non valida';
    }
  };

  const columns = useMemo(() => [
    { key: 'status', label: 'Stato', sortable: true },
    { key: 'jobType', label: 'Tipo', sortable: true },
    { key: 'printerName', label: 'Stampante', sortable: true },
    { key: 'orderDisplayNumber', label: 'Ordine', sortable: true },
    { key: 'createdAt', label: 'Creato il', sortable: true },
    { key: 'lastAttemptAt', label: 'Ultimo Tentativo', sortable: true },
    { key: 'retryCount', label: 'Tentativi', sortable: true },
    { key: 'errorMessage', label: 'Errore', sortable: false },
  ], []);

  const renderCell = (job: PrintJobDto, columnKey: string) => {
    switch (columnKey) {
      case 'status':
        return renderStatusBadge(job.status);
      case 'jobType':
        return renderJobType(job.jobType);
      case 'printerName':
        return job.printerName;
      case 'orderDisplayNumber':
        return job.orderDisplayNumber || 'N/A';
      case 'createdAt':
        return formatDate(job.createdAt);
      case 'lastAttemptAt':
        return formatDate(job.lastAttemptAt);
      case 'retryCount':
        return <div className="text-center">{job.retryCount}</div>;
      case 'errorMessage':
        return <div className="max-w-xs truncate" title={job.errorMessage || ''}>{job.errorMessage || 'Nessun errore'}</div>;
      default:
        return null;
    }
  };

  const renderActions = (job: PrintJobDto) => (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button variant="ghost" className="h-8 w-8 p-0">
          <MoreHorizontal className="h-4 w-4" />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end">
        <DropdownMenuItem
          onClick={() => handleRetryJob(job.id)}
          disabled={isRetrying === job.id || job.status !== PrintJobStatus.Failed}
        >
          {isRetrying === job.id ? (
            <><RefreshCw className="mr-2 h-4 w-4 animate-spin" /> Riprovo...</>
          ) : (
            <><RefreshCw className="mr-2 h-4 w-4" /> Riprova Manualmente</>
          )}
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );

  return (
    <div className="space-y-6">
      <Card>
        <CardHeader>
          <CardTitle>Monitoraggio Processi di Stampa</CardTitle>
          <CardDescription>
            Visualizza lo stato di tutti i processi di stampa. I processi falliti verranno ritentati automaticamente.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <PaginatedTable
            storageKey="printJobs"
            columns={columns}
            paginatedData={printJobs}
            isLoading={isLoading}
            error={error}
            queryParams={queryParams}
            onQueryChange={handleQueryChange}
            renderCell={renderCell}
            renderActions={renderActions}
            itemKey={(job) => job.id}
          />
        </CardContent>
      </Card>
    </div>
  );
}
