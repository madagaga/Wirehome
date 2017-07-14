#include "Infrared.h"

IRrecvPCI Infrared::myReceiver = IRrecvPCI(2);
IRdecode Infrared::myDecoder;

void Infrared::Init()
{
	Infrared::myReceiver.enableIRIn();
}

void Infrared::ProcessLoop()
{
	if (Infrared::myReceiver.getResults()) 
	{
		uint8_t codeProtocol = UNKNOWN;
		uint32_t codeValue = 0;
		uint8_t codeBits = 0;

    	myDecoder.decode();
		codeProtocol = myDecoder.protocolNum;

		if(codeProtocol == UNKNOWN)
		{
			//The raw time values start in decodeBuffer[1] because
			//the [0] entry is the gap between frames. The address
			//is passed to the raw send routine.
			codeValue=(uint32_t)&(recvGlobal.decodeBuffer[1]);
			//This isn't really number of bits. It's the number of entries
			//in the buffer.
			codeBits=recvGlobal.decodeLength-1;

			for(bufIndex_t i=1; i<recvGlobal.recvLength; i++) 
			{
				Serial.print(recvGlobal.recvBuffer[i],DEC);
			}
		}
		else 
		{
    		if (myDecoder.value == REPEAT_CODE) 
			{
      			// Don't record a NEC repeat value as that's useless.
    		}
			else 
			{
      			codeValue = myDecoder.value;
      			codeBits = myDecoder.bits;

				uint8_t messageSize = sizeof(codeProtocol)+sizeof(codeValue)+sizeof(codeBits)+1;
				Serial.write(messageSize);
				Serial.write(RS_ACTION_Infrared);
				Serial.write(codeProtocol);
				Serial.write(codeValue);
				Serial.write(codeBits);
    		}
  		}
    	myReceiver.enableIRIn();
  	}
}

void Infrared::Send(uint8_t package[], uint8_t packageLength)
{
	
}



