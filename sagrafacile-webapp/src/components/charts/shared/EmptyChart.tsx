import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { BarChart3, AlertCircle } from "lucide-react";

interface EmptyChartProps {
    title: string;
    message?: string;
    isError?: boolean;
    height?: number;
}

export function EmptyChart({ 
    title, 
    message = "Nessun dato disponibile", 
    isError = false, 
    height = 300 
}: EmptyChartProps) {
    return (
        <Card>
            <CardHeader>
                <CardTitle className="flex items-center gap-2">
                    {isError ? (
                        <AlertCircle className="h-5 w-5 text-destructive" />
                    ) : (
                        <BarChart3 className="h-5 w-5 text-muted-foreground" />
                    )}
                    {title}
                </CardTitle>
            </CardHeader>
            <CardContent>
                <div 
                    className="flex flex-col items-center justify-center text-muted-foreground"
                    style={{ height: `${height}px` }}
                >
                    {isError ? (
                        <AlertCircle className="h-12 w-12 mb-4 text-destructive" />
                    ) : (
                        <BarChart3 className="h-12 w-12 mb-4" />
                    )}
                    <p className="text-center text-sm">{message}</p>
                    {isError && (
                        <p className="text-center text-xs mt-2 text-destructive">
                            Riprova più tardi o contatta l'assistenza
                        </p>
                    )}
                </div>
            </CardContent>
        </Card>
    );
}
